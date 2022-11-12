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
				target = move(this->garbage.back());	// targetに所有権を委譲すれば, whileブロックの終わりでデストラクタが呼ばれて解放される.
				this->garbage.pop_back();
			}	// このブロックがないと, targetが解放されるまでgarbageのロックが外れなくなり, GCを別スレッドで動かしている意味がなくなる.
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