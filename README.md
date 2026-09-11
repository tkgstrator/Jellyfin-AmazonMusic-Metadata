# Jellyfin AmazonMusic Metadata

Amazon Music のカタログから曲・アルバム・アーティストのメタデータとアートワークを
取得して Jellyfin に保存するプラグイン。**日本と US の両マーケットプレイスに対応**
することを狙う。

姉妹プラグインの [Jellyfin-AppleMusic-Metadata][apple] と同じ設計・同じリリース
経路を共有する。

## 現状

**カタログへの到達手段が未確定のため、まだメタデータは取得しない。** この初期
コミットで入っているのは、Apple Music 版で実運用に耐えた足場だけ。

| 層 | 状態 |
| --- | --- |
| プラグイン本体（`Plugin.cs`、設定画面、DI） | 動く |
| 外部 ID（曲・アルバム・アーティスト・マーケットプレイス） | 動く |
| 応答キャッシュ（メモリ上限つき LRU + ディスク永続化 + 同時リクエストの束ね） | 動く |
| スロットル（直列化・間隔・429 のクールダウン） | 動く |
| `ICatalogTransport`（生 JSON を返す契約） | 定義済み |
| `IAmazonMusicCatalog`（検索と ID 引きの契約） | 定義済み・型引数は未確定 |
| **ネットワーク transport** | **未着手** |
| DTO（応答スキーマ） | 未着手 |
| メタデータ / 画像プロバイダ | 未着手 |

## 最初に決めること

**Amazon Music には Apple の `amp-api` に相当する公開カタログ API が無い。**
Apple Music 版は `music.apple.com` の JS バンドルから Web プレイヤー用トークンを
取り出して公式 JSON API を叩く方式で成立したが、Amazon で同じ手が使えるかは
未調査。少なくとも次のどれを採るかを決めないと先に進めない。

1. **Amazon Music の Web プレイヤーが使う内部 API** — 認証方式・安定性・利用条件を
   実測する必要がある。
2. **自前バックエンド経由** — 資格情報をプラグインに持たせず中継する。Apple 版の
   [docs/backend.md](docs/backend.md) と同じ契約に寄せられる。
3. **Product Advertising API** — 商品情報としては引けるが、アルバムのトラック一覧や
   アーティストの経歴が取れるかは要確認。アソシエイト登録も要る。

決まったら `ICatalogTransport` の実装を 1 つ足し、`PluginServiceRegistrator.Compose`
に渡せば、キャッシュとスロットルはそのまま効く。

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
