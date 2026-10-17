# Desktop Cat / デスクトップねこ

[English](README.md) | [简体中文](README.zh-CN.md) | **日本語**

Windows のデスクトップを歩き回り、毛づくろいしたり、ジャンプしたり、突然走り出したり、丸くなって眠ったりする小さなねこです。C#、Windows Forms、GDI+ で描画し、なめらかな向きの変化、つまみ上げたときのリアクション、マウス追従、鳴き声などに対応しています。

[公式サイト・ダウンロード](https://desktop-cat.aitoast.cn/) · [不具合の報告](https://github.com/KaMiYiLu/desktop-cat/issues)

このリポジトリではアプリのソースコードを公開しています。プロジェクトのライセンスはまだ確定していません。詳細は [ソースコードの出典とライセンスの確認状況](SOURCE-STATUS.md)（中国語）をご覧ください。現在、Windows アプリの表示言語は中国語です。紹介サイトは中国語と英語に対応しています。

## 主な機能

- ねこをマウスの左ボタンで押したままにすると、つまみ上げられます。離すとランダムなリアクションを見せます。
- お散歩、前足の毛づくろい、その場ジャンプ、前方へのジャンプ、ランニング、ダッシュ、泥の足跡、前足を抱えたお昼寝。
- 歩きながら片耳や両耳をぴくぴく動かします。立ち止まると、しっぽを揺らしたりマウスを眺めたりします。
- コントロールパネルから、マウス追従、マウスへの攻撃、つまみ上げ操作、サウンド全体、足音を個別に切り替えられます。
- 初期設定ではマウスへの攻撃を許可しています。マウス追従と定期的なランダム攻撃は無効です。ねこを下ろしたあとのランダムな行動でマウスを追いかける場合があります。不要な場合はコントロールパネルで無効にできます。
- 起動時にはねこだけが表示されます。システムトレイのアイコンを右クリックし、**打开控制台**（コントロールパネルを開く）を選んで操作画面を表示します。終了するには **Esc を約 3 秒間長押し**するか、トレイメニューから終了を選びます。
- 設定は実行ファイルの隣にある `config.ini` に保存されます。通常の動作ではログファイルを作成しません。

## ビルドと起動

Windows、PowerShell、システムの .NET Framework 4.x C# コンパイラが必要です。開発環境でのビルドは確認済みですが、各 Windows バージョン、高 DPI、マルチモニター環境での互換性テストはまだ完了していません。ビルドや実行に紹介サイト、元の Desktop Goose の実行ファイル、非公開サーバーは必要ありません。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\artifacts\bin\DesktopCat-v71.exe
```

バージョン番号は [VERSION](VERSION) で一元管理しています。EXE のバージョン情報と配布ファイル名はスクリプトが生成します。初回ビルドでは `config.example.ini` から設定をコピーし、次回以降は出力先にある既存の設定を保持します。出力先を変更する場合は、`build.ps1` の `OutputDirectory` パラメーターを指定してください。

## 動作確認と配布パッケージ

```powershell
# 描画、ポーズ、小さな動き、毛づくろい、配布パッケージの検証
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1

# デスクトップ環境で UI、つまみ上げ、動作の完了、ログ非生成を追加検証
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1 -Desktop

# 音声出力デバイスがある環境で、無音サンプルを使って音声バッファを検証
powershell -ExecutionPolicy Bypass -File .\tools\test.ps1 -Audio

powershell -ExecutionPolicy Bypass -File .\tools\package-release.ps1
```

配布ファイルは `artifacts/release` に出力されます。ユーザー向け ZIP に含まれるのは `DesktopCat.exe`、`config.ini`、`使用说明.txt`（中国語の利用ガイド）の 3 ファイルだけです。SHA-256 のチェックサムファイルは ZIP の外に出力されます。診断コマンドを明示的に実行した場合のみ、`artifacts` 以下にテスト結果を保存します。

レビュー用のソースアーカイブを作成するには、次のコマンドを実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\export-source.ps1
```

アーカイブに含めるファイルは [SOURCE-FILES.txt](SOURCE-FILES.txt) に明記されています。出力先は `artifacts/source` です。配布対象のソースファイルを追加するときは、この一覧も更新してください。README は英語・簡体字中国語・日本語の 3 言語版を含みます。ソースアーカイブと、一般ユーザー向けの 3 ファイル入りパッケージは別のものです。

[Windows CI ワークフロー](.github/workflows/ci.yml) は GitHub 上でビルド、検証、パッケージ作成を行います。リリースの自動公開は行わず、紹介サイトのデプロイ用認証情報も必要ありません。基本的な検証は GitHub の Windows ランナーで通過済みです。デスクトップ操作と音声バッファの検証はローカル環境でも実施しています。

## ディレクトリ構成

```text
src/                    アプリのソースコードと診断用エントリーポイント
Assets/                 ビルドに必要なアイコン、音声、テキスト
tools/                  配布・検証スクリプトとアイコン生成用コード
docs/USER-GUIDE.txt      利用ガイドのテンプレート（中国語）
VERSION                 バージョン番号
config.example.ini      個人設定を含まない初期設定
README.md               英語版 README
README.zh-CN.md         簡体字中国語版 README
README.ja.md            日本語版 README
THIRD-PARTY-NOTICES.md   音声素材の作者と出典
SOURCE-STATUS.md         ソースコードの出典とライセンスの確認状況
```

## インスピレーションとクレジット

[samperson 氏の Desktop Goose](https://samperson.itch.io/desktop-goose) に着想を得た、独立した非公式のねこアレンジ作品です。元の作者からの公認や推奨を意味するものではありません。第三者の音声素材については [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) をご覧ください。アプリのコードおよびアイコンの最終的なライセンス範囲は、引き続き確認中です。

不具合報告や機能の提案は [Issues](https://github.com/KaMiYiLu/desktop-cat/issues) で受け付けています。アプリのバージョン、問題が発生した操作、エラーメッセージを添えてください。変更を提案する際は [CONTRIBUTING.md](CONTRIBUTING.md)（中国語）もご確認ください。
