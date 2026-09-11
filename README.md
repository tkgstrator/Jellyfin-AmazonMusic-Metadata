# Jellyfin AmazonMusic Metadata

Amazon Music のカタログから曲・アルバム・アーティストのメタデータとアートワークを
取得して Jellyfin に保存するプラグイン。**日本と US の両マーケットプレイスに対応**
することを狙う。

姉妹プラグインの [Jellyfin-AppleMusic-Metadata][apple] と同じ設計・同じリリース
経路を共有する。

## 現状

Amazon Music の公開 Web Player が使う**匿名 GraphQL 経路**を検証している。2026-09-11の
静的解析により、desktop Webの検索は`tenzingTextSearch`を通常のGraphQL HttpLinkへ送り、
iOS/AndroidだけがApollo local handlerで内部REST endpointへ変換することを確認した。調査記録は
[docs/web-player-graphql.md](docs/web-player-graphql.md) にまとめている。

この経路は公開 API ではなく Web Player の内部実装である。JPではGraphQLによる曲・アルバム・
アーティストのID引きを実通信で確認した。desktop Webの匿名検索経路は静的に確認済みで、live
testによる最終確認を残している。Amazonアカウントやaccount Cookie/tokenを要求する方式は採用しない。

| 層 | 状態 |
| --- | --- |
| プラグイン本体（`Plugin.cs`、設定画面、DI） | 動く |
| 外部 ID（曲・アルバム・アーティスト・マーケットプレイス） | 動く |
| 応答キャッシュ（メモリ上限つき LRU + ディスク永続化 + 同時リクエストの束ね） | 動く |
| スロットル（直列化・間隔・429 のクールダウン） | 動く |
| `ICatalogTransport`（生 JSON を返す契約） | GraphQL POST 対応済み |
| `IAmazonMusicCatalog`（検索と ID 引きの契約） | 定義済み・型引数は未確定 |
| 匿名 Web Player bootstrap / GraphQL transport | 実装済み・JPの曲/アルバム/アーティストID引きをlive確認済み |
| 匿名検索 | desktop GraphQL経路を実装済み・live確認待ち |
| DTO（応答スキーマ） | ID 引き operation 単位で実装予定 |
| メタデータ / 画像プロバイダ | 未着手 |

## カタログ取得方針

Web Player の guest config と公開 JavaScript bundle から、その時点の endpoint、app
version、device type、匿名 application key を実行時に取得し、詳細取得用の Apollo GraphQL
を呼ぶ。JPでは曲・アルバム・アーティストの詳細取得とアルバム収録曲をlive確認済みで、アーティストは
匿名で許可されたID・名前・画像だけを使う。application key の実値はソース、設定、fixture、ログへ保存しない。

desktop Webの検索は`tenzingTextSearch`を匿名GraphQLとして送る。空のPanda tokenは失敗条件では
なく、Web Playerがdevice/session/territoryと匿名client IDからheaderを構築する。iOS/Androidの
同名operationはlocal handlerがRESTへ変換する別経路である。アートワークURLはサイズ置換せず
opaqueな値としてJellyfinへ渡し、IDとmarketplaceを必ず対で保存する。

内部 API の変更や利用条件には注意が必要である。fixture ベースの unit test に加えて、
通常 CI から除外した live test で bundle と schema の変更を検出する。

## ビルド・テスト

```bash
dotnet build                          # net9.0 と net10.0 の両方
dotnet test                           # xunit v3 / Microsoft.Testing.Platform
dotnet format --verify-no-changes     # CI と同じ書式チェック
./scripts/package.sh [version]        # dist/ にリリース成果物
```

`net9.0` が Jellyfin 10.11 の ABI、`net10.0` が Jellyfin 12.0 の ABI。リリースは
ABI ごとに別 zip で出す。

## 設定

| 設定 | 意味 |
| --- | --- |
| Marketplaces | 問い合わせるマーケットプレイスの順（既定 `jp` → `us`） |
| Language override | 言語タグの上書き。既定はマーケットプレイスに追従 |
| Max search results | 検索の取得件数 |
| Artwork size | アートワークの一辺（px） |
| Request interval | リクエストの最小間隔（ミリ秒） |
| Cache | 有効・寿命・メモリ上限・ディスクに書く上限サイズ |

## ライセンス

GPLv3。Jellyfin のバイナリ NuGet パッケージにリンクするため、コンパイル後の
プラグインは GPLv3 になる。

[apple]: https://github.com/tkgstrator/Jellyfin-AppleMusic-Metadata
