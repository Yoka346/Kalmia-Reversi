#pragma once

#include <vector>
#include <thread>
#include <chrono>

#include "node.h"

namespace search::mcts
{
	/**
	* @class
	* @brief 不要になったNodeオブジェクトを溜めて, まとめて破棄するガベージコレクタ.
	* @detail 仕組みはLeela Chess Zeroのノードの破棄の方法と同じ. サブツリーのルートノードを解放して, 
	* その直下のノードは, unique_ptr<Node>のデストラクタによって連鎖的に解放する.
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
