#pragma once

#include <vector>
#include <thread>
#include <chrono>

#include "node.h"

namespace search::mcts
{
	/**
	* @class
	* @brief �s�v�ɂȂ���Node�I�u�W�F�N�g�𗭂߂�, �܂Ƃ߂Ĕj������K�x�[�W�R���N�^.
	* @detail �d�g�݂�Leela Chess Zero�̃m�[�h�̔j���̕��@�Ɠ���. �T�u�c���[�̃��[�g�m�[�h���������, 
	* ���̒����̃m�[�h��, unique_ptr<Node>�̃f�X�g���N�^�ɂ���ĘA���I�ɉ������.
	**/
	class NodeGarbageCollector
	{
	public:
		NodeGarbageCollector() : garbage(), worker_thread(std::thread([this]() { worker(); })) { ; }
		~NodeGarbageCollector() { this->stop_flag.store(true); this->worker_thread.join(); }

		void add(std::unique_ptr<Node> node);
		void collect();

	private:
		static constexpr int32_t COLLECT_INTERVAL_MS = 100;

		std::vector<std::unique_ptr<Node>> garbage;

		std::thread worker_thread;
		std::mutex garbage_mutex;
		std::atomic<bool> stop_flag = false;

		void worker();
	};
}
