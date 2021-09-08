# Kalmia-Reversi
C#(.net core 3.1)で書かれたリバーシの思考エンジンです。探索アルゴリズムにUCT(Upper Confidence Bound applied Tree)を採用していることが特徴です。  
囲碁プログラムでよく用いられるGTP(Go Text Protocol)に対応しているため、GoGuiで対戦することが可能です(方法は後述)。  
現在は試作段階のPrototypeがリリースされています。棋力は有段者程度と見込んでいますが、正確に何段かは作者にリバーシの素養が無いため分かりません。

## 1. 動作環境
最低動作環境
+ OS : Windows, Linux, macOS
+ CPU : SSE2に対応したもの(ほとんどのCPUが対応しているため特に気にする必要はありません)
+ メモリ : 4GB

推奨環境
+ OS : Windows, Linux, macOS (いずれも64bit対応)
+ CPU : 64bit、4コア以上、AVX2に対応したもの(2013~2015年以降に購入したPCであれば概ね対応しています)
+ メモリ : 8GB以上

このソフトウェアはmacOSに対応していますが、M1チップ搭載のMacでは動作確認ができていません。またARMのCPUにおいても動作確認ができておりません。

## 2. ダウンロード
[Release](https://github.com/Yoka346/Kalmia-Reversi/releases)からKalmia本体とReversiRulerをダウンロードしてください。ReversiRulerはGoGuiでリバーシをプレイするために必要です。
KalmiaとReversiRulerの実行には.net core 3.1のランタイムが必要です。[ここ](https://dotnet.microsoft.com/download/dotnet/3.1)からインストーラーをダウンロードし、インストールしてください。
GoGuiは[ここ](https://github.com/Remi-Coulom/gogui/releases)からダウンロード可能です。ここ以外の場所からダウンロードしたGoGuiの場合はリバーシに対応できない可能性があります。

## 3. GoGuiへの登録
まず、ダウンロードしたKalmia本体とReversiRulerを適当な場所に配置し展開します。

![キャプチャ](https://user-images.githubusercontent.com/53616737/132211712-a2bbcb9a-cf22-4be1-9822-2ef1072b99f3.PNG)

次にGoGuiを起動し、上部のツールバーから 対局 -> ルール -> 新規プログラム　を選択します。「囲碁プログラムの選択」から展開したフォルダ内にあるReversiRuler.exe(LinuxやmacOS版では".exe"が付いていません)を選択し、OKを選択します。
![キャプチャ](https://user-images.githubusercontent.com/53616737/132212273-d823a48a-c929-44b9-8b77-140fe29dabc6.PNG)

これでGoGuiでリバーシをプレイできるようになります。
次に上部のツールバーから プログラム -> 新規プログラム を選択します。ReversiRulerを導入した時と同様に、「囲碁プログラムの選択」から展開したフォルダ内にあるKalmia.exe(LinuxやmacOS版では".exe"が付いていません)を選択します。
![キャプチャ](https://user-images.githubusercontent.com/53616737/132213450-b0a5ab70-e27d-44cd-8ccf-dbcf5abcf7fa.PNG)

次にコマンドテキストボックスで".../Kalmia.exe --level {0~10の整数値}"という形式でレベルを指定します。レベルの指定を行わなかった場合はデフォルトでレベル5が適用されます。このままOKを選択すればプログラムの登録は完了です。(レベルについての詳細は後述)

![キャプチャ](https://user-images.githubusercontent.com/53616737/132213577-6dcb07af-9965-4f2e-8e22-46bb1f460313.PNG)

登録が完了した次回以降は 対局 -> ルール -> プログラムの起動 からReversiRulerを起動し、 プログラム -> プログラムの起動 からKalmiaを起動すれば対局可能な状態になります。

## 4. GoGuiでの遊び方
ReversiRulerとGoGuiが起動した時点では以下のような初期局面が表示されています。このまま黒石を置けばKalmiaが白番となり思考および着手を行います。Kalmiaに黒番をにぎらせたい場合は 対局 -> コンピュータの手番 から手番を選択してください。
![キャプチャ](https://user-images.githubusercontent.com/53616737/132214173-a7072512-482a-4546-bc27-ac11b37c139e.PNG)

自分の石を置ける場所がないときはF2キーを押下、または 対局 -> パス からパスをすることが可能です。また、Kalmiaに石を置ける場所がないときは自動的にパスされます。Kalmiaがパスした際に以下のようなウインドウが表示されますが、対局は続いているのでそのまま着手を続けてください。

![キャプチャ](https://user-images.githubusercontent.com/53616737/132501018-1e45ebea-f382-4450-ab40-188c24cfa2f0.PNG)

両者とも石を置く場所が無くなった場合は終局となります。終局した場合はパスを行うと以下のように結果が表示されます。※終局してもパスを行わないと結果が表示されません。

![キャプチャ](https://user-images.githubusercontent.com/53616737/132501377-997e96b7-ab30-4979-9d01-e0e890c1fe52.PNG)

## 4. 難易度
Kalmiaはlevel0からlevel10の11段階の難易度に分かれています。レベルが上がるほど棋力が上がります。各難易度の詳細は以下の通りです。

+ level 0  
シミュレーション回数: 10  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 無効  
ポンダリング: 無効  

+ level 1  
シミュレーション回数: 100  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 無効  
ポンダリング: 無効  

+ level 2  
シミュレーション回数: 800  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 3  
シミュレーション回数: 3200  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 4  
シミュレーション回数: 32000  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 5  
シミュレーション回数: 320000  
着手の選択方法: それぞれの手の勝率に応じて確率的に選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 6  
シミュレーション回数: 320000  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 7  
シミュレーション回数: 1000000  
着手の選択方法: それぞれの手の勝率に応じて確率的に選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 8  
シミュレーション回数: 1000000  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 9  
シミュレーション回数: 3200000  
着手の選択方法: それぞれの手の勝率に応じて確率的に選択する  
探索木の再利用: 有効  
ポンダリング: 無効  

+ level 10  
シミュレーション回数: 3200000  
着手の選択方法: 最も勝率の高い手を選択する  
探索木の再利用: 有効  
ポンダリング: 有効  

注意: level 10ではメモリが足りなくなる場合があります。

シミュレーション回数とは、「1回の思考で読む局面数」という認識で構いません。例えば、シミュレーション回数が100回であれば100局面を読んだうえで次の一手を決定します。  

着手の選択方法には2種類あります。1つは最も高い勝率の手を選択する方法。もう1つがそれぞれの手の勝率に応じて確率的に選択する方法です。後者の意味は、基本的には勝率の高い手を高確率で選択するが、勝率の低い手も低確率で選択することがあるという意味です。  

探索木の再利用とは、前回の思考で読んだ局面を次の思考のときにも利用するという意味です。  

ポンダリングとは、相手の手番中も思考を続けるという意味です。  

## 5. Kalmiaの着手の特徴
Kalmiaは最終的な石差ではなく、勝率を予測しながら先を読んでいきます。それ故に大差をつけて勝とうとはしません。例えば、相手の石をすべて消すことができるような手順があったとしても、それを選択しない場合があります。また同様の理由で、Kalmiaは勝勢になると緩い手を打ち始めます。逆に劣勢になると、相手のミスを期待するような無理な手を打ち始めます。






