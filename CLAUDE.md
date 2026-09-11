# CLAUDE.md

このリポジトリで作業するエージェント向けの指針。

## プロジェクトの目的

Amazon Music のカタログから曲・アルバム・アーティストのメタデータとアートワークを
取得して Jellyfin に保存するプラグイン。**日本と US の両マーケットプレイスに対応**
することを狙う。

姉妹プラグイン [Jellyfin-AppleMusic-Metadata][apple] と設計・CI・リリース経路を
共有する。判断に迷ったら向こうの実装を見る。あちらは実ライブラリ 2.5 万曲で運用
した実績がある。

利用者向けの説明は [README.md](README.md)、バックエンドの契約は
[docs/backend.md](docs/backend.md)、開発手順は [docs/development.md](docs/development.md)。

## カタログ取得方式

**公開Web Playerの匿名GraphQL経路を使う。** JPでは2026-09-11の実通信で曲・アルバム・
アーティストの詳細とアルバム収録曲を確認した。`album.tracks`はconnectionではなくTrackの配列で、
アーティストは`followerCount`・`biography`・`tracks`が匿名では権限エラーになる。desktop Webの
`tenzingTextSearch`は通常のGraphQL HttpLinkへ流れ、空Panda token時もdevice/session/territoryと
匿名client IDから認証headerを構築する。iOS/Androidだけが同名operationをlocal handlerでRESTへ
変換する。desktop検索のlive確認は未実施である。実測と制約は
[docs/web-player-graphql.md](docs/web-player-graphql.md) に記録する。

公開 API の契約ではないため、bootstrap、transport、operation 固有 parser を分離し、opt-in
live test で変更を検出する。Amazon アカウント、account Cookie/token、再生用 token は扱わない。
匿名 application key の実値をソース、設定、fixture、cache key、例外、ログへ保存しない。

WebページのHTML DOMをメタデータ源としてスクレイピングしない。entry HTMLはbundle URLの
discoveryだけに使い、カタログデータはAPI responseから取得する。検索はdesktop WebのGraphQL
operationとして実装し、mobile専用REST経路と混同しない。ID引きはlive確認済みのoperationと
fieldだけをproviderへ接続する。

## 決定済みの設計（勝手に変えない）

1. **カタログの取得元は差し替え可能にする。** 差分は「ベース URL」と
   「認証ヘッダの与え方」の 2 点に閉じ込め、DTO・パース・フォールバック・
   アートワーク URL 生成は全方式で共有する。
2. **マーケットプレイスは優先順＋フォールバック。** 設定の順（既定 `jp` → `us`）で
   問い合わせ、見つからなければ次へ。言語はマーケットプレイスに追従。設定で上書き可。
   GraphQL error、壊れた応答、401/403/429 を「見つからない」に変換して次へ進まない。
3. **アートワーク URL は opaque。** GraphQL が返した URL を加工しない。検索 converter が
   width/height を後付けする場合があるため、寸法をサーバー実測値とはみなさない。
4. **マルチターゲット。** `net9.0` = Jellyfin 10.11 ABI、`net10.0` = Jellyfin 12.0 ABI。
   両方をビルドし、リリースは ABI ごとに別 zip。
5. **リリース成果物は `scripts/package.sh` が作る。** jprm / `build.yaml` は使わない。
   メタ情報は `scripts/meta.template.json` が単一の出所。
6. **UI 文言（設定画面）は英語。** README / docs / コミットメッセージ本文は日本語でよい。

## リポジトリ構造

```
.devcontainer/            Dev Container（app + Jellyfin 12.0 + Jellyfin 10.11）
.github/workflows/        integration.yaml, deployment.yaml
docs/                     backend.md（バックエンド契約）, development.md（開発手順）
scripts/                  package.sh, manifest.py, deploy.sh, meta.template.json
Jellyfin.Plugin.AmazonMusic/
  Plugin.cs               BasePlugin<PluginConfiguration>, IHasWebPages
  PluginServiceRegistrator.cs  キャッシュの DI 登録と Compose()
  Configuration/          PluginConfiguration.cs, configPage.html（埋め込みリソース）
  Catalog/                API クライアント層（Jellyfin 非依存）
    Caching/              応答キャッシュと同時リクエストの束ね
    Throttling/           直列化・間隔・429 のクールダウンと再試行
    ICatalogTransport.cs  生 JSON を返す契約
    IAmazonMusicCatalog.cs  検索と ID 引きの契約（型引数は DTO 確定後に閉じる）
  ExternalIds/            ProviderKeys, 3 つの IExternalId, IExternalUrlProvider
tests/Jellyfin.Plugin.AmazonMusic.Tests/
Directory.Build.props     バージョンと TFM → Jellyfin バージョン/ABI の対応表
```

## 引き継いだ設計判断（Apple 版の実測に基づく）

**`Catalog/` は `MediaBrowser.*` を参照しない。** サーバー上でステップ実行できない
事情があるため、ロジックはここに寄せてユニットテストで検証する。

**カタログ ID はマーケットプレイス単位。** ID を保存するときは必ず
`ProviderKeys.Marketplace` も一緒に書く。jp で見つけた ID を us に問い合わせると
別物を掴む。

**`ICatalogTransport` は生の JSON を返す。** デシリアライズはカタログ層の責務。
こうしてあるのは、キャッシュがレスポンスをそのまま保存でき、シリアライズの
往復が要らないため。

**429 は `null` ではなく `CatalogRateLimitedException` で伝える。** `null` は
「無かった」として一定時間キャッシュされるため、制限中の未回答を `null` にすると、
その間に触った曲が全部「カタログに無い」扱いで固定される（Apple 版 v0.1.1 で
実際に起きた）。

**スロットルは `ThrottledCatalogTransport` が担う。** 構成は
cache → throttle → network。**並列にしない・間隔を空ける**。429 後はクールダウンを
倍々にしつつ再試行し、上限に達したら待たずに諦める。検索と ID 引きは別々に止める
（Apple では検索だけが長時間 429 になり、ID 引きは通り続けた）。

**キャッシュは `CachingCatalogTransport` が担う。** 効果は 2 つあり、片方だけでは
不十分。同じ URL を二度取りに行かないことと、ライブラリスキャンで同一アルバムの
問い合わせが同時に何本も飛ぶのを 1 本に束ねること。

**メモリ上限はバイト単位で持つ（件数ではない）。** 検索応答と ID 引きでサイズが
桁違いのため。**大きい応答はディスクに書かない**（既定 8 KB 超）。ディスクは
1 エントリ 1 ファイルで、キーの SHA-256 先頭 2 文字でシャーディングする。

SQLite は使わない。Jellyfin はプラグインが再利用できる SQLite アセンブリを提供して
おらず、自前で参照するとサーバーが既に読み込んでいるものと衝突する恐れがある。

## ビルド・テスト

```bash
dotnet build                          # net9.0 と net10.0 の両方
dotnet build -f net10.0               # 片方だけ
dotnet test                           # xunit v3 / Microsoft.Testing.Platform
dotnet format --verify-no-changes     # CI と同じ書式チェック
./scripts/deploy.sh [--legacy]        # 開発用 Jellyfin に反映して再起動
./scripts/package.sh [version]        # dist/ にリリース成果物
```

### 環境まわりの既知の事情

- **テスト実行には .NET 9 と .NET 10 の両ランタイム（ASP.NET Core 含む）が要る。**
- **`dotnet test` は .NET 10 SDK で VSTest が使えない。** `global.json` で
  Microsoft.Testing.Platform に opt-in している。テストプロジェクトは
  `<OutputType>Exe</OutputType>` かつ xunit v3。
- **プラグイン本体は Jellyfin 参照を `ExcludeAssets=runtime` で参照する。**
  テストプロジェクトは通常参照する。

## コーディング規約

- `TreatWarningsAsErrors=true`。警告を残さない。
- **public メンバーには XML ドキュメントコメントを書く。**
- **コード内のコメントと識別子は英語。** 日本語はドキュメントと会話のみ。
- ログは Serilog 形式の構造化ログ。文字列連結や補間で組み立てない。
- Jellyfin のバージョン差分は `#if JELLYFIN_10_11` / `#if JELLYFIN_12_0` で吸収する。

## ブランチとリリース

```
feature/*  ──PR──▶  develop  ──マージ──▶  master  ──v* タグ──▶  正式リリース
```

- ワークフローは `integration.yaml` と `deployment.yaml` の 2 つだけ。
- `deployment.yaml` は `integration.yaml` を `workflow_call` で呼んでから公開する。
- **manifest は ABI ごと・チャンネルごとに分ける。** まとめると 12.0 サーバーが
  net9.0 を候補に入れ、安定版利用者が自動更新で dev を掴む。

## コミット

Conventional Commits（`.commitlintrc.yaml`、header は 128 文字まで）。
type は `build/ui/ci/docs/feat/fix/perf/refactor/revert/format/test/chore`。

## やらないこと

- Amazon の資格情報・API キーをこのリポジトリに置かない。設定値としてプラグインに
  持たせない（バックエンドの責務）。
- **Web ページの HTML スクレイピングをしない。**
- Jellyfin のランタイムアセンブリを配布物に含めない。
- `dist/`, `bin/`, `obj/`, `media/` の中身をコミットしない。

[apple]: https://github.com/tkgstrator/Jellyfin-AppleMusic-Metadata
