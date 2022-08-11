#pragma once

namespace utils
{
	/**
	* @class
	* @brief クラスの静的イニシャライザ.
	* @tparam (T) ターゲットのクラス.
	* @tparam (func) 初期化処理を記述した関数のポインタ.
	* @detail  コンストラクタ内でfuncを呼び出すため, Tクラスの宣言の後にStaticInitilizer<T, func>型の変数を宣言することで,
	* funcの処理が実行される.
	**/
	template<class T, void (*func)()>
	class StaticInitializer
	{
	public:
		StaticInitializer() { func(); };
	};
}