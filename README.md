# Jellyfin AmazonMusic Metadata

Amazon Music のカタログから曲・アルバム・アーティストのメタデータとアートワークを
取得して Jellyfin に保存するプラグイン。**日本と US の両マーケットプレイスに対応**
することを狙う。

姉妹プラグインの [Jellyfin-AppleMusic-Metadata][apple] と同じ設計・同じリリース
経路を共有する。

## 現状

Amazon Music の公開 Web Player が使う匿名カタログ経路を利用する。2026-09-12の
ブラウザ実測により、desktop Webの検索は`showSearch` BFFへ送られ、詳細取得は匿名GraphQLを
使うことを確認した。調査記録は[docs/web-player-graphql.md](docs/web-player-graphql.md)に
まとめている。

この経路は公開 API ではなく Web Player の内部実装である。JPでは検索と、曲・アルバム・
アーティストのID引きをlive testで確認済み。Amazonアカウントやaccount Cookie/tokenを
要求する方式は採用しない。

| 層 | 状態 |
| --- | --- |
| プラグイン本体（`Plugin.cs`、設定画面、DI） | 動く |
| 外部 ID（曲・アルバム・アーティスト・マーケットプレイス） | 動く |
| 応答キャッシュ（メモリ上限つき LRU + ディスク永続化 + 同時リクエストの束ね） | 動く |
| スロットル（直列化・間隔・429 のクールダウン） | 動く |
| `ICatalogTransport`（生 JSON を返す契約） | POST 対応済み |
| `IAmazonMusicCatalog`（検索と ID 引きの契約） | 実装済み |
| 匿名 Web Player bootstrap / GraphQL transport | 実装済み・JPの曲/アルバム/アーティストID引きをlive確認済み |
| 匿名検索 | desktop `showSearch` BFF経路を実装済み・live確認済み |
| DTO / parser | operation単位で実装済み |
| メタデータ / 画像プロバイダ | 曲・アルバム・アーティスト、アルバム/アーティスト画像を実装済み |

## カタログ取得方針

Web Player の guest config と公開 JavaScript bundle から、その時点の endpoint、app
version、device type、匿名 application key を実行時に取得し、詳細取得用の Apollo GraphQL
を呼ぶ。JPでは曲・アルバム・アーティストの詳細取得とアルバム収録曲をlive確認済みで、アーティストは
匿名で許可されたID・名前・画像だけを使う。application key の実値はソース、設定、fixture、ログへ保存しない。

desktop Webの検索は`showSearch` BFFへ送る。検索語、guest session、device、CSRFなどの
Web Player contextは観測済みの二重JSON形式でrequest bodyへ格納するが、runtime値をcacheや
ログへ残さない。詳細取得は匿名GraphQLを使う。アートワークURLはサイズ置換せずopaqueな値として
Jellyfinへ渡し、IDとmarketplaceを必ず対で保存する。

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
| Request timeout | カタログ呼び出しのタイムアウト（秒） |
| Request interval | リクエストの最小間隔（ミリ秒） |
| Cache | 有効・寿命・メモリ上限・ディスクに書く上限サイズ |

## ライセンス

GPLv3。Jellyfin のバイナリ NuGet パッケージにリンクするため、コンパイル後の
プラグインは GPLv3 になる。

[apple]: https://github.com/tkgstrator/Jellyfin-AppleMusic-Metadata
