# Kalmia-Reversi
C#(.NET6.0)で書かれたリバーシの思考エンジンです。探索アルゴリズムにUCT(Upper Confidence Bound applied Trees)を採用していることが特徴です。  
囲碁プログラムでよく用いられるGTP(Go Text Protocol)に対応しているため、GoGuiで対戦することが可能です(方法は後述)。  
現在の最新バージョンは1.0です。

## 1. 動作環境
最低動作環境
+ OS : Windows, Linux, macOS
+ CPU : SSE2に対応したもの(ほとんどのCPUが対応しているため特に気にする必要はありません)
+ メモリ : 4GB

推奨環境
+ OS : Windows 64bit
+ CPU : 64bit、4コア以上、AVX2に対応したもの(2013~2015年以降に購入したPCであれば概ね対応しています)
+ メモリ : 8GB以上

このソフトウェアはLinux, macOSに対応してはいますが、Windowsに比べて動作が遅くなる傾向があります。また、ARMプロセッサ上での動作は確認できていません。

## 2. ダウンロード
[Release](https://github.com/Yoka346/Kalmia-Reversi/releases)からKalmia本体をダウンロードしてください。Version 1.0が最新です。
Kalmiaの実行には.NET6.0のランタイムが必要です。[ここ](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)からインストーラーをダウンロードし、インストールしてください。
GoGuiは[ここ](https://github.com/Remi-Coulom/gogui/releases)からダウンロード可能です。ここ以外の場所からダウンロードしたGoGuiの場合はリバーシに対応できない場合があります。

## 3. GoGuiへの登録
まず、ダウンロードしたKalmia本体を適当な場所に配置し展開します。できる限りユーザーフォルダー内(Documentsなど)に配置してください。Cドライブ直下などでは権限が足りずにエラーが発生することがあります。どうしてもCドライブ直下などに配置したい場合は、GoGuiを管理者として実行してください。

![スクリーンショット 2022-03-19 220914](https://user-images.githubusercontent.com/53616737/159122398-13feafa3-5e45-4101-ad22-24271f78164a.png)

次にGoGuiを起動し、上部のツールバーから 対局 -> ルール -> 新規プログラム　を選択します。「囲碁プログラムの選択」から、展開したフォルダ内にあるKalmia.exe(LinuxやmacOS版では".exe"が付いていません)を選択し、コマンドテキストボックスの末尾に"--mode ruler"というオプションを追加します。ワーキングディレクトリにはKalmia.exeが存在するディレクトリを指定します。そのままOKをクリックすればReversiRulerの導入が完了です。  
※ Prototypeでは、ReversiRulerはKalmiaとは別のプログラムでしたが、バージョン1.0でKalmiaと統合されました。

![スクリーンショット 2022-03-19 221255](https://user-images.githubusercontent.com/53616737/159122573-537ab28a-5fa5-415c-9411-fe3f65597226.png)

![スクリーンショット 2022-03-19 221546](https://user-images.githubusercontent.com/53616737/159122775-712e3e33-473f-4712-8314-c95d4d45161b.png)


これでGoGuiでリバーシをプレイできるようになりました。
次に上部のツールバーから プログラム -> 新規プログラム を選択します。ReversiRulerを導入した時と同様に「囲碁プログラムの選択」から、展開したフォルダ内にあるKalmia.exeを選択します。
次にコマンドテキストボックスの末尾に"--mode gtp --difficulty [難易度]"を追加します。難易度は"easy", "normal", "professional", "superhuman", "custom"から選択できます(難易度の詳細については後述)。また、ワーキングディレクトリには、Kalmia.exeがあるディレクトリを指定してください。

![スクリーンショット 2022-03-19 222159](https://user-images.githubusercontent.com/53616737/159122828-bdc54251-a8fb-4f19-83e1-135f841989da.png)

登録が完了した次回以降は 対局 -> ルール -> プログラムの起動 からReversiRulerを選択し、 プログラム -> プログラムの起動 からKalmiaを選択すれば対局可能な状態になります。

## 4. GoGuiでの遊び方
ReversiRulerとGoGuiが起動した時点では以下のような初期局面が表示されています。このままでは通常のリバーシの盤面とは向きが異なるので、表示 -> Board OrientationからFlip Horizontallyを選択します。
![スクリーンショット 2022-03-20 152104](https://user-images.githubusercontent.com/53616737/159150784-51acbbcd-299d-4625-a5ca-0d88e2f37cc6.png)

盤面の向きを調整し、このまま黒石を置けばKalmiaが白番となり思考および着手を行います。Kalmiaに黒番をにぎらせたい場合は 対局 -> コンピュータの手番 から手番を選択してください。
![スクリーンショット 2022-03-20 152323](https://user-images.githubusercontent.com/53616737/159150839-341a85bf-cf45-416e-aa0b-12fa6adb5941.png)


自分の石を置ける場所がないときはF2キーを押下、または 対局 -> パス からパスをすることが可能です。また、Kalmiaに石を置ける場所がないときは自動的にパスされます。Kalmiaがパスした際に以下のようなウインドウが表示されますが、対局は続いているのでそのまま着手を続けてください。

![キャプチャ](https://user-images.githubusercontent.com/53616737/132501018-1e45ebea-f382-4450-ab40-188c24cfa2f0.PNG)

両者とも石を置ける場所が無くなった場合は終局となります。最終手を人間が着手した場合、盤面が正しく更新されない場合がありますが、石数のカウントは正しく行われます。正しい盤面を表示したい場合は、ツール -> GTPシェル でシェルを開き、テキストボックスに"gogui-rules_final_result"というコマンドを入力することで正しく表示されます。

![キャプチャ](https://user-images.githubusercontent.com/53616737/132501377-997e96b7-ab30-4979-9d01-e0e890c1fe52.PNG)

## 4. 難易度
Kalmiaは"easy", "normal", "professional", "superhuman"の4段階の難易度に分かれています。オプションの難易度で"custom"という難易度も用意されていますが、これについては後に説明します。各難易度の設定は以下の通りです。

+ easy  
プレイアウト回数: 20  
強さ: 初心者向け

+ normal  
プレイアウト回数: 100  
強さ: リバーシ中級者向け

+ proffesional  
プレイアウト回数: 3200  
強さ: リバーシ上級者向け

+ superhuman  
プレイアウト回数: 320000  
強さ: リバーシ高段者向け

プレイアウト回数とは、「1回の思考で読む局面数」のようなものです。例えば、プレイアウト回数が100回であれば100局面を読んだうえで次の一手を決定します。  

## 5. 難易度のカスタマイズ
Kalmiaでは4つの基本難易度の他に、ユーザーが自由に強さをカスタマイズできる難易度"custom"が存在します。難易度"custom"の設定内容は"/difficulty/custom.json"に記述されています。ここではそれぞれの設定項目について説明します。

+ PlayoutCount  
1手にかけるプレイアウト回数です。基本的にこの値を大きくすればするほど棋力が向上します。  

+ LatencyCentisec  
思考エンジンからGUIへ着手を送るときの推定遅延(単位は0.01秒)です。この値を大きくすると、持ち時間より余裕をもって思考を切り上げるようになります。この設定項目は持ち時間ありの対局(後述)の際に有効になります。  

+ OpeningMoveNum  
初期局面から何手目までを序盤と定義するかを決める項目です。例えば15を設定すると、初期局面から15手目までを序盤と定義します。持ち時間ありの対局の際は、Kalmiaは序盤にあまり時間を使わないように着手します。  

+ SelectMoveStochastically  
この項目をtrueに設定すると、確率的に着手を決定するようになります。確率的な着手を有効にすると、高確率で有利な手を選択しますが、低確率で不利な手も選択するようになります。  

+ StochasticMoveNum  
初期局面から何手目まで確率的な着手行うかを決める項目です。例えば15をすると、初期局面から15手目までは確率的な着手を行います。この項目はSelectMoveStochasticallyがtrueに設定されている際に有効となります。  

+ SoftmaxTemperature  
確率的な着手を行う際のソフトマックス温度です。この値を1より大きくすればするほどより局面が変化しやすくなる反面、不利な手も選ばれやすくなります。逆に1より小さくすればするほど(0に近づくほど)最も有利な手が選ばれやすくなります。0に設定すると、最善手が必ず選ばれるようになります。この項目はSelectMoveStochasticallyがtrueに設定されている際に有効となります。  

+ ReuseSubtree  
この項目をtrueに設定すると、前回の探索結果を次回の探索結果に引き継ぐようになります。  

+ EnablePondering  
この項目をtrueに設定すると、ポンダリングが有効になります。ポンダリングとは相手の手番中も思考を続けることを指します。ポンダリングを有効にすると棋力は向上しますが、CPU使用率とメモリ使用率が非常に高くなります。この項目はReuseSubtreeがtrueになっている際に有効です。  

+ ValueFuncParamFile  
価値関数(評価関数)のパラメータが格納されているファイルパスです。特に変更はしないでください。  

+ TreeOptions  
探索に関するオプションです。"ThreadNum", "NodeNumLimit", "ManagedMemoryLimit"の3つの項目で構成されます。  

+ ThreadNum  
探索スレッド数です。0を設定するとCPUの論理コア数の分だけスレッドを作成します。  

+ NodeNumLimit  
探索木のノード数の上限値です。ノード数がこの上限値を超えた場合、探索を打ち切ります。  

+ ManagedMemoryLimit  
メモリの使用量の上限値です(単位はbyte)。メモリ使用量(厳密には.NETランタイムによって管理されるメモリ使用量)がこの値を超えた場合、探索を打ち切ります。  

+ MateSolverMoveNum  
終盤必勝探索を行うタイミングを設定します。例えば20を設定すると、空きマスの数が20以下になった際に必勝探索を開始します。大きな値にしてしまうと終盤にフリーズする恐れがあります。  

+ FinalDiscDifferenceSolverMoveNum  
最終石差探索を行うタイミングを設定します。例えば18を設定すると、空きマスの数が18になった際に最終石差探索を開始します。大きな値にしてしまうと終盤でフリーズする恐れがあります。  

+ EndgameSolverMemorySize  
終盤必勝探索と最終石差探索の際に使用するメモリサイズ(単位はbyte)を設定します。このメモリ領域は置換表のために用いられます。大きくすればするほど探索効率が向上することが期待できますが、あまり大きくしすぎるとスワッピングが発生し大幅に低速化します。  

## 6. 持ち時間の設定
Kalmiaは持ち時間の設定に対応しています。GoGuiで持ち時間を設定するには 対局 -> 対局情報 から対局情報編集ウインドウを開きます。そのウインドウの"持ち時間"の項目で設定が可能です。

## 7. Kalmiaについての詳細
Kalmiaについての詳細は以下のブログで紹介しています。  
https://kalmia.hatenadiary.jp/entry/2022/03/18/140704

## 8. 謝辞
以下の文献及びリポジトリは開発の際に大変参考になりました。

+ 文献  
http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm  
ビットボードによる合法手生成の参考にしました。  
https://deepmind.com/research/publications/2019/mastering-game-go-deep-neural-networks-tree-search  
https://deepmind.com/research/publications/2019/mastering-game-go-without-human-knowledge  
MCTSのロールアウトの代わりに、静的評価関数を用いるというアイデアはこちらの論文から着想を得ました。  
  
+ リポジトリ  
https://github.com/LeelaChessZero/lc0  
https://github.com/TadaoYamaoka/DeepLearningShogi  
https://github.com/yaneurao/YaneuraOu/tree/master/source/engine/dlshogi-engine  
主にMCTSの実装の参考にしました。  
https://github.com/abulmo/edax-reversi  
https://github.com/okuhara/edax-reversi-AVX  
ビットボードによる合法手生成、盤面のハッシュコードの生成などの参考にしました。




