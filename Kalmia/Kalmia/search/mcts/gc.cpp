#include "gc.h"

using namespace std;
using namespace std::chrono;

namespace search::mcts
{
	void NodeGarbageCollector::add(std::unique_ptr<Node> node)
	{
		if (!node)
			return;
		lock_guard<mutex> lock(this->garbage_mutex);
		this->garbage.emplace_back(move(node));
	}

	void NodeGarbageCollector::collect()
	{
		while (!this->stop_flag.load())
		{
			std::unique_ptr<Node> target;	

			{
				lock_guard<mutex> lock(this->garbage_mutex);
				if (this->garbage.empty())
					return;
				target = move(this->garbage.back());	// target�ɏ��L�����Ϗ������, while�u���b�N�̏I���Ńf�X�g���N�^���Ă΂�ĉ�������.
				this->garbage.pop_back();
			}	// ���̃u���b�N���Ȃ���, target����������܂�garbage�̃��b�N���O��Ȃ��Ȃ�, GC��ʃX���b�h�œ������Ă���Ӗ����Ȃ��Ȃ�.
		}
	}

	void NodeGarbageCollector::worker()
	{
		while (!this->stop_flag.load())
		{
			this_thread::sleep_for(milliseconds(COLLECT_INTERVAL_MS));	
			collect();	
		}
	}
}