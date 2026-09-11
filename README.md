# Jellyfin AmazonMusic Metadata

Amazon Music のカタログから曲・アルバム・アーティストのメタデータとアートワークを
取得して Jellyfin に保存するプラグイン。**日本と US の両マーケットプレイスに対応**
することを狙う。

姉妹プラグインの [Jellyfin-AppleMusic-Metadata][apple] と同じ設計・同じリリース
経路を共有する。

## 現状

Amazon Music の公開 Web Player が使う**匿名 GraphQL 経路**を、ID 引きの候補として
検証している。2026-09-11 の静的解析と実通信により、検索は GraphQL ではなく Apollo の
local handler が Amazon 内部の REST endpoint へ変換することが判明した。調査記録は
[docs/web-player-graphql.md](docs/web-player-graphql.md) にまとめている。

この経路は公開 API ではなく Web Player の内部実装である。匿名での検索は成立していないが、
JPではGraphQLによる曲・アルバム・アーティストのID引きを実通信で確認した。アーティストは
匿名許可field（ID・名前・画像）に限定する。Amazon アカウントや account Cookie/token を要求する方式は採用しない。

| 層 | 状態 |
| --- | --- |
| プラグイン本体（`Plugin.cs`、設定画面、DI） | 動く |
| 外部 ID（曲・アルバム・アーティスト・マーケットプレイス） | 動く |
| 応答キャッシュ（メモリ上限つき LRU + ディスク永続化 + 同時リクエストの束ね） | 動く |
| スロットル（直列化・間隔・429 のクールダウン） | 動く |
| `ICatalogTransport`（生 JSON を返す契約） | GraphQL POST 対応済み |
| `IAmazonMusicCatalog`（検索と ID 引きの契約） | 定義済み・型引数は未確定 |
| 匿名 Web Player bootstrap / GraphQL transport | 実装済み・JPの曲/アルバム/アーティストID引きをlive確認済み |
| **匿名検索** | **未成立（REST token を取得できない）** |
| DTO（応答スキーマ） | ID 引き operation 単位で実装予定 |
| メタデータ / 画像プロバイダ | 未着手 |

## カタログ取得方針

Web Player の guest config と公開 JavaScript bundle から、その時点の endpoint、app
version、device type、匿名 application key を実行時に取得し、詳細取得用の Apollo GraphQL
を呼ぶ。JPでは曲・アルバム・アーティストの詳細取得とアルバム収録曲をlive確認済みで、アーティストは
匿名で許可されたID・名前・画像だけを使う。application key の実値はソース、設定、fixture、ログへ保存しない。

検索は REST `textsearch/search/v1_1` と `x-amz-access-token` を使う別経路である。guest
session で `/pandaToken` を呼んでも token は空だったため、匿名検索は未成立であり実装しない。
アートワーク URL はサイズ置換をせず opaque な値として Jellyfin へ渡す。ID 引きが成立した
場合も、ID と marketplace を必ず対で保存する。

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
