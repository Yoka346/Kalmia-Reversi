# Kalmia-Reversi
C#(.net core 3.1)で書かれたリバーシの思考エンジンです。探索アルゴリズムにUCT(Upper Confidence Bound applied Tree)を採用していることが特徴です。
囲碁プログラムでよく用いられるGTP(Go Text Protocol)に対応しているため、GoGuiで対戦することが可能です(方法は後述)。

## 1. 動作環境
最低動作環境
+ OS : Windows, Linux, macOS
+ CPU : SSE2に対応したもの(ほとんどのCPUが対応しているため特に気にする必要はありません)
+ メモリ : 4GB

推奨環境
+ OS : Windows, Linux, macOS (いずれも64bit対応)
+ CPU : 64bit、4コア以上、AVX2に対応したもの(2013~2015年以降に購入したPCであれば概ね対応しています)
+ メモリ : 8GB以上

このソフトウェアはmacOSに対応していますが、M1搭載のMacでは動作確認ができていません。

## 2. ダウンロード
[Release](ここにURLを入れる)からKalmia本体とReversiRulerをダウンロードしてください。ReversiRulerはGoGuiでリバーシをプレイするために必要です。
KalmiaとReversiRulerの実行には.net core 3.1のランタイムが必要です。[ここ](https://dotnet.microsoft.com/download/dotnet/3.1)からインストーラーをダウンロードし、インストールしてください。
GoGuiは[ここ](https://github.com/Remi-Coulom/gogui/releases)からダウンロード可能です。ここ以外の場所からダウンロードしたGoGuiの場合はリバーシに対応できない可能性があります。

## 3. GoGuiへの登録
まず、ダウンロードしたKalmia本体とReversiRulerを適当な場所に配置し展開します。

![キャプチャ](https://user-images.githubusercontent.com/53616737/132211712-a2bbcb9a-cf22-4be1-9822-2ef1072b99f3.PNG)

次にGoGuiを起動し、上部のツールバーから 対局 -> ルール -> 新規プログラム　を選択します。「囲碁プログラムの選択」から展開したフォルダ内にあるReversiRuler.exeを選択し、OKを選択します。
![キャプチャ](https://user-images.githubusercontent.com/53616737/132212273-d823a48a-c929-44b9-8b77-140fe29dabc6.PNG)

これでGoGuiでリバーシをプレイできるようになります。
次に上部のツールバーから プログラム -> 新規プログラム を選択します。ReversiRulerを導入した時と同様に、「囲碁プログラムの選択」から展開したフォルダ内にあるKalmia.exeを選択します。
![キャプチャ](https://user-images.githubusercontent.com/53616737/132213450-b0a5ab70-e27d-44cd-8ccf-dbcf5abcf7fa.PNG)

次にコマンドテキストボックスで".../Kalmia.exe --level 0~10の数字"という形式でレベルを指定します。レベルの指定を行わなかった場合はデフォルトでレベル5が適用されます。このままOKを選択すればプログラムの登録は完了です。
![キャプチャ](https://user-images.githubusercontent.com/53616737/132213577-6dcb07af-9965-4f2e-8e22-46bb1f460313.PNG)

登録が完了した次回以降は 対局 -> ルール -> プログラムの起動 からReversiRulerを起動し、 プログラム -> プログラムの起動 からKalmiaを起動すれば対局可能な状態になります。

## 4. GoGuiでの遊び方
ReversiRulerとGoGuiが起動した時点では以下のような初期局面が表示されています。このまま黒石を置けばKalmiaが白番となり思考および着手を行います。Kalmiaに黒番をにぎらせたい場合は 対局 -> コンピュータの手番 から手番を選択してください。
![キャプチャ](https://user-images.githubusercontent.com/53616737/132214173-a7072512-482a-4546-bc27-ac11b37c139e.PNG)

自分の石を置ける場所がないときはF2キーまたは 対局 -> パス からパスをすることが可能です。また、Kalmiaが置ける場所がないときは自動的にパスされます。Kalmiaがパスした際に以下のようなウインドウが表示されますが、対局は続いているのでそのまま着手を続けてください。




