# Kalmia-Reversi
C#(.net core 3.1)で書かれたリバーシの思考エンジンです。探索アルゴリズムにUCT(Upper Confidence applied Tree)を採用していることが特徴です。
囲碁プログラムでよく用いられるGTP(Go Text Protocol)に対応しているため、GoGuiで対戦することが可能です(方法は後述)。

## 1. ダウンロード
[Release](ここにURLを入れる)からKalmia本体とReversiRulerをダウンロードしてください。ReversiRulerはGoGuiでリバーシをプレイするために必要です。
GoGuiは[ここ](https://github.com/Remi-Coulom/gogui/releases)からダウンロード可能です。

## 2. 動作環境
最低動作環境
+ OS : Windows, Linux
+ CPU : SSE2に対応したもの(ほとんどのCPUが対応しているため特に気にする必要はありません)
+ メモリ : 4GB

推奨環境
+ OS : Windows, Linux (いずれも64bit対応)
+ CPU : 64bit、4コア以上、AVX2に対応したもの(2013~2015年以降に購入したPCであれば概ね対応しています)
+ メモリ : 8GB以上

## 3. GoGuiへの登録
まず、ダウンロードしたKalmia本体とReversiRulerを適当な場所に配置し展開します。
