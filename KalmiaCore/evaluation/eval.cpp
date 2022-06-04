#pragma once
#include "eval.h"

using namespace std;

using namespace evaluation;
using namespace reversi;

// public
EvalParamsFileHeader::EvalParamsFileHeader(const string& label, int32_t version, DateTime& releasedTime) :label(label), version(version), released_time(releasedTime)
{
	if (this->label.size() > LABEL_SIZE)
	{
		char buffer[256];
		snprintf(buffer, sizeof(buffer), "The length of \"label\" must be within %d-byte", LABEL_SIZE);
		throw std::length_error(buffer);
	}
}

EvalParamsFileHeader::EvalParamsFileHeader(const string path)
{
	char buffer[HEADER_SIZE];
	load(path, buffer);
	init(buffer);
}

EvalParamsFileHeader::EvalParamsFileHeader(ifstream& ifs)
{
	char buffer[HEADER_SIZE];
	load(ifs, buffer);
	init(buffer);
}

void EvalParamsFileHeader::write_to_file(const string& path)
{
	ofstream ofs(path);
	ofs.exceptions(ofstream::eofbit | ofstream::failbit | ofstream::badbit);
	write_to_stream(ofs);
}

void EvalParamsFileHeader::write_to_stream(ostream& stream)
{
	stream.seekp(0, ios_base::beg);
	stream.write(this->label.c_str(), this->label.size());
	stream.seekp(LABEL_SIZE - this->label.size(), ios_base::cur);
	stream.write((char*)&this->version, VERSION_SIZE);

	char buffer[DATE_TIME_SIZE];
	datetime_to_buffer(this->released_time, buffer);
	stream.write(buffer, DATE_TIME_SIZE);
}

// private static
DateTime EvalParamsFileHeader::load_datetime(const char* buffer)
{
	auto year = ((uint16_t*)buffer)[0];
	auto month_to_second = buffer + sizeof(uint16_t);
	return DateTime(year, month_to_second[0], month_to_second[1], month_to_second[2], month_to_second[3], month_to_second[4]);
}

void EvalParamsFileHeader::datetime_to_buffer(DateTime& datetime, char* buffer)
{
	stringstream mem_stream(buffer);
	auto datetime_array = datetime.as_array();
	auto year = (uint16_t)datetime_array[0];
	mem_stream.write((char*)&year, sizeof(year));
	for (int i = 0; i < 5; i++)
	{
		char c = datetime_array[i];
		mem_stream.write(&c, 1);
	}
}

void EvalParamsFileHeader::load(const string path, char* buffer)
{
	ifstream ifs(path);
	load(ifs, buffer);
}

void EvalParamsFileHeader::load(ifstream& ifs, char* buffer)
{
	ifs.seekg(0, ios_base::beg);
	ifs.read(buffer, HEADER_SIZE);
}

// private
void EvalParamsFileHeader::init(const char* buffer)
{
	this->label.assign(buffer + LABEL_OFFSET, LABEL_SIZE);
	this->version = *(int32_t*)(buffer + VERSION_OFFSET);
	this->released_time = load_datetime(buffer + DATE_TIME_OFFSET);
}


// public
EvalFunction::EvalFunction(const string& label, int32_t version, int32_t move_count_per_stage) 
	: move_count_per_stage(move_count_per_stage), weight()
{
	auto dt_now = DateTime::get_now();
	this->header = EvalParamsFileHeader(label, version, dt_now);
	this->stage_num = ((SQUARE_NUM - 4) / this->move_count_per_stage) + 1;
	for (int32_t color = 0; color < 2; color++)
	{
		auto w = this->weight[color] = new float* [this->stage_num];
		for (int32_t stage = 0; stage < this->stage_num; stage++)
			w[stage] = new float[FEATURE_VEC_LEN];
	}
}

EvalFunction::EvalFunction(const string& path) : weight()
{
	ifstream ifs(path);
	this->header = EvalParamsFileHeader(ifs);
	auto packed_weight = load_packed_weight(ifs, this->stage_num);
	this->move_count_per_stage = (SQUARE_NUM - 4) / (this->stage_num - 1);
	for (int32_t color = 0; color < 2; color++)
	{
		this->weight[color] = new float* [this->stage_num];
		for (int32_t stage = 0; stage < this->stage_num; stage++)
			this->weight[color][stage] = new float[FEATURE_VEC_LEN];
	}
	expand_packed_weight(packed_weight);
}

EvalFunction::EvalFunction(const EvalFunction& eval_func) 
	: header(eval_func.header), stage_num(eval_func.stage_num), move_count_per_stage(eval_func.move_count_per_stage), weight()
{
	for (int32_t color = 0; color < 2; color++)
	{
		this->weight[color] = new float* [this->stage_num];
		for (int32_t stage = 0; stage < this->stage_num; stage++)
		{
			this->weight[color][stage] = new float[FEATURE_VEC_LEN];
			memmove(this->weight[color][stage], eval_func.weight[color][stage], sizeof(float) * FEATURE_VEC_LEN);
		}
	}
}

EvalFunction::~EvalFunction()
{
	for (int32_t color = 0; color < 2; color++)
	{
		float** w = this->weight[color];
		for (int32_t stage = 0; stage < this->stage_num; stage++)
			delete w[stage];
		delete w;
	}
}

inline float EvalFunction::f(BoardFeature& bf)
{
	return f((SQUARE_NUM - 4 - bf.get_empty_square_count()) / this->move_count_per_stage, bf);
}

inline float EvalFunction::f(int32_t stage, BoardFeature& bf)
{
	auto score = 0.0f;
	uint16_t* pattenrs = bf.patterns;
	auto color = bf.get_side_to_move();
	float* weight = this->weight[color][stage];
	for (int32_t i = 0; i < FEATURE_NUM; i++)
		score += weight[pattenrs[i] + PATTERN_OFFSET[i]];
	return mathfunction::std_sigmoid(score);
}

//private
shared_ptr<shared_ptr<shared_ptr<float>>> EvalFunction::load_packed_weight(ifstream& ifs, int32_t& stage_num)
{
	ifs.seekg(EvalParamsFileHeader::HEADER_SIZE, ios_base::beg);
	char tmp;
	ifs.read(&tmp, 1);
	stage_num = tmp;
	shared_ptr<shared_ptr<shared_ptr<float>>> weight(new shared_ptr<shared_ptr<float>>[stage_num]);
	char buffer[sizeof(float)];
	for (int32_t stage = 0; stage < stage_num; stage++)
	{
		weight.get()[stage] = shared_ptr<shared_ptr<float>>(new shared_ptr<float>[FEATURE_KIND_NUM]);
		auto w = weight.get()[stage];
		for (int32_t kind = 0; kind < FEATURE_KIND_NUM; kind++)
		{
			const int32_t PAT_NUM = PACKED_PATTERN_NUM[kind];
			auto ww = w.get()[kind] = shared_ptr<float>(new float[PAT_NUM]);
			for (int32_t i = 0; i < PAT_NUM; i++)
			{
				ifs.read(buffer, sizeof(float));
				ww.get()[i] = *(float*)buffer;
			}
		}
	}
	return weight;
}

void EvalFunction::expand_packed_weight(shared_ptr<shared_ptr<shared_ptr<float>>> packed_weight)
{
	int32_t i;
	for (int32_t stage = 0; stage < this->stage_num; stage++)
	{
		int32_t offset = 0;
		auto w_b = this->weight[DiscColor::BLACK][stage];
		auto w_w = this->weight[DiscColor::WHITE][stage];
		for (int32_t kind = 0; kind < FEATURE_KIND_NUM - 1; kind++)
		{
			auto pw = packed_weight.get()[stage].get()[kind].get();
			for (int32_t pattern = 0; pattern < PATTERN_NUM[kind]; pattern++)
			{
				auto idx = pattern + offset;
				auto symmetric_idx = TO_SYMMETRIC_PATTERN_IDX[idx];
				if (symmetric_idx < idx)
					w_b[idx] = w_b[symmetric_idx];
				else
					w_b[idx] = pw[i++];
				w_w[TO_OPPONENT_PATTERN_IDX[idx]] = w_b[idx];
			}
			offset += PATTERN_NUM[kind];
		}

		// bias
		w_b[offset] = *packed_weight.get()[stage].get()[FEATURE_KIND_NUM - 1].get();
		w_w[offset] = *packed_weight.get()[stage].get()[FEATURE_KIND_NUM - 1].get();
	}
}