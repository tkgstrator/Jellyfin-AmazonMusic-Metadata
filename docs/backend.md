# バックエンド方針

## 現行構成

このプラグインの現行版に自前バックエンドは不要である。Amazon Musicの公開Web Playerが
未ログインの訪問者に提供する匿名bootstrapを実行し、Web Player内部のGraphQL endpointへ
プラグインから直接問い合わせる。

```text
Jellyfin provider
  → AmazonMusicCatalog
    → CachingCatalogTransport
      → ThrottledCatalogTransport
        → WebPlayerGraphQlTransport
          → Amazon Music Web Player GraphQL
```

確認済みのbootstrapとGraphQL仕様は
[web-player-graphql.md](web-player-graphql.md)に記録している。

## 認証情報を持たない

この経路はAmazonアカウントへのログインを必要としない。プラグインは次の情報を要求、保存、
または送信しない。

- AmazonアカウントのCookie
- Amazonアカウントのaccess token
- 再生用token
- Developer Token
- MusicKitの秘密鍵（`.p8`）

匿名リクエストの`x-api-key`は、公開Web PlayerのJavaScript bundleから実行時に取得する
application identifierである。値は設定、ソースコード、fixture、ディスクcacheへ保存せず、
ログや例外にも出力しない。device IDとsession IDもプロセス内だけで保持する。

Developer Token、MusicKit、`.p8`はApple Musicの認証方式であり、Amazon Musicの匿名Web
Player GraphQLには関係しない。

## 内部APIとしての制約

利用しているGraphQLは公開API契約ではなく、Amazon Music Web Playerの内部実装である。
endpoint、operation、schema、header、bundle構造は予告なく変わる可能性がある。このため、
次の境界を分離する。

- endpointと匿名headerの解決: `WebPlayerGraphQlTransport`
- HTTP requestの表現: `CatalogRequest`
- raw JSONのparseとmarketplace fallback: `AmazonMusicCatalog`
- Jellyfin型への変換: metadata/image provider

bundle変更はunit fixtureだけでは検出できないため、リリース前にopt-in live testも実行する。

## 将来のバックエンドtransport

将来、内部APIの変更吸収、複数インスタンス間の負荷制御、または運用上の理由で自前
バックエンドを追加する可能性はある。ただし、現時点では未実装であり、現行版の動作要件
ではない。

バックエンドを追加する場合も、catalogとproviderの契約は変えず、`ICatalogTransport`の
network実装だけを差し替える。

```text
CatalogRequest
  ├─ HTTP method
  ├─ relative URL
  ├─ GraphQL request body
  ├─ stable cache key
  └─ request kind (Lookup; Search is reserved)
        ↓
ICatalogTransport.SendAsync(...)
        ↓
raw GraphQL JSONまたは明示的なnot-found
```

バックエンドtransportは、プラグインが作成した検索・詳細取得用GraphQL operationとvariablesを
上流へ中継し、レスポンス本文を再整形せず返す。こうすることで、operation固有DTO、parse、
marketplace fallback、cache、throttleを直接接続時と共有できる。mobile専用のREST検索経路は
この契約の対象にしない。

## 将来の中継契約

### Request

- Method: `POST`
- Content-Type: `application/json`
- Body: `operationName`、`variables`、`query`を持つGraphQL envelope
- Marketplace: request pathまたは専用headerで明示
- Localeとterritory: variablesまたは専用headerで明示

中継バックエンド自身を保護する認証方式は、実装時に別途決める。Amazon Music Web Playerの
匿名`x-api-key`を利用者に入力させたり、プラグイン設定へ保存したりしてはならない。

### Response

- 成功: 上流のraw GraphQL JSONをそのまま返す
- 明示的なresource不在: `404`、または契約で定めたnot-found応答
- rate limit: `429`
- 認証・bootstrap失敗: `401`または`403`
- 上流障害: 対応する`5xx`

ステータスを不在へ変換しない。特に`401`、`403`、`429`、`5xx`、GraphQL `errors`を
`404`や空の成功responseに変えると、プラグインがnegative cacheへ保存し、未回答を
「カタログに存在しない」と誤認する。

### ログ

次をログへ出力しない。

- `x-api-key`の値
- Cookieまたはaccount token
- device IDとsession ID
- request header全体
- GraphQL body全体

operation名、marketplace、安全なcache key、HTTP status、秘密情報を含まないGraphQL errorの
要約だけを構造化ログへ記録する。

## 非対象

現行transportと将来のバックエンドtransportのどちらでも、次は対象外とする。

- ユーザーライブラリ、プレイリスト、フォロー状態の操作
- 楽曲再生、stream URL、DRM
- 購入またはアカウント操作
- HTML DOMのスクレイピング
- artwork URLの推測による書き換え

このプラグインが扱うのは、匿名で取得できる曲・アルバム・アーティストのカタログ
metadataと、GraphQLが返すopaqueなartwork URLだけである。
