# Amazon Music Web Player GraphQL 調査

調査日: 2026-09-11

## 結論

Amazon Music の公開 Web Player は、未ログインの訪問者にも匿名セッションを発行する。
詳細取得には Apollo GraphQL の匿名経路が存在する一方、検索は GraphQL HTTP endpointへ
送られず、Apollo local handler が内部 REST endpoint へ変換する。REST検索に必要なtokenは
通常のguest bootstrapでも空だったため、アカウントなしの検索は成立していない。

これは公開 API の契約ではなく Web Player の内部実装である。bundle、operation、schema、
endpoint、認証 header は予告なく変わる可能性がある。GraphQLによるID引きもまだlive
検証を終えておらず、unit fixtureに加えてopt-in live testで成立を確認する必要がある。

## 調査方法

`https://music.amazon.co.jp/search` が返す HTML、`/config.json`、公開 JavaScript bundle
を静的に調べた。ログイン、再生、購入、ライブラリ変更は行っていない。ブラウザでの
Network 記録は調査環境に Playwright/Chromium がなかったため未実施であり、実装時の
live test で補う。

調査に使った完全な HTML と bundle は Git 管理しない。config に含まれた IP address、
device/session ID、CSRF 値と、bundle 内の有効な匿名 application keyも保存しない。

## Bootstrap

JP Web Player では次の応答を確認した。

1. 公開ページが `main.<hash>.js` を読み込む。
2. `POST /config.json?skipToken=false` が guest runtime config を返す。
3. config の `dragonflyBundle` がカタログ実装を含む `dragonfly.<hash>.js` を指す。
4. config と bundle から GraphQL endpoint、app version、device type、匿名 application keyを得る。

確認時の匿名configは次の地域情報を返した。

| 項目 | JP |
| --- | --- |
| `marketplaceId` | `A1VC38T7YXB528` |
| `musicTerritory` | `JP` |
| `displayLanguage` | `ja_JP` |
| `siteRegion` | `FE` |

`accessToken` と `customerId` は空だった。device/session ID と CSRF 値はリクエストごとの
runtime値であり、資料や設定へ固定しない。

## GraphQL transport

Web Player は Apollo GraphQL の HTTP POST を使う。通常bodyは次のenvelopeで、既定経路は
persisted queryではなく完全なquery documentを送る。

```json
{
  "operationName": "operationName",
  "variables": {},
  "query": "query operationName { ... }"
}
```

確認時の通常 endpoint は `https://gql.music.amazon.dev` だった。feature flag
`isDragonflyFFCountryDomainEnabled` が有効な場合は国別 endpointを使い、JPでは
`https://gql.music.amazon.co.jp` となる。endpointはconfigとfeature flagから毎回解決し、
固定しない。

匿名経路で確認したheader名は次のとおり。

- `x-api-key`
- `x-amzn-device-id`
- `x-amzn-device-type`
- `x-amzn-session-id`
- `music-territory`
- `Accept-Language`
- `x-amzn-client-app-version`
- `x-amzn-trace-start`
- `Content-Type: application/json`

`x-api-key` は公開bundleに埋め込まれた匿名Web Player用application identifierから実行時に
取得する。値をsource、設定、fixture、cache key、例外、ログへ書かない。GraphQL経路では
SigV4などのrequest signingは確認されなかった。

Cookie認証経路と `Authorization: AmznMusic ...` を使うaccount token経路もbundle内に存在
するが、プラグインでは使用しない。

## 詳細取得用GraphQL operation

| 用途 | operation |
| --- | --- |
| アルバム基本情報 | `getAlbumMetadata`, `albumMetadata` |
| アルバム収録曲 | `getAlbumTracks` |
| 曲のID引き | `trackMetadata` |
| アーティスト概要 | `getArtistSummary`, `getArtistByAsin` |

これらはbundle内で確認したが、匿名guestで各operationが許可されるかはlive test未確認である。

## 検索はREST

UIはApollo上で`tenzingTextSearch`を発行するが、Web Playerのlocal linkが横取りして次のREST
requestへ変換する。

```text
POST https://music.amazon.com/{region}/api/textsearch/search/v1_1/
X-Amz-Target: com.amazon.tenzing.textsearch.v1_1.TenzingTextSearchServiceExternalV1_1.search
x-amz-access-token: <runtime token>
Content-Encoding: amz-1.0
```

JPの`region`は`FE`である。GraphQL endpointへ`tenzingTextSearch`を直接送ると、input typeと
query fieldが存在しないschema errorになった。

Web PlayerのIdentity初期化は同一originの`GET /pandaToken`を呼ぶ。cookieなしの直接呼出しと、
通常ページ→config→`/pandaToken`をguest cookie付きで再現した場合の両方でHTTP 200になったが、
`accessToken`は空だった。そのため匿名REST検索は成立しておらず、account token等を要求する
方式で回避しない。

REST検索backendのresource discriminatorとして以下を確認した。

- `com.amazon.music.platform.model#CatalogAlbum`
- `com.amazon.music.platform.model#CatalogArtist`
- `com.amazon.music.platform.model#CatalogTrack`

主なIDはASINであり、アーティストでは`localAsin`も使われる。IDと取得可能な内容は
marketplace/territoryに依存する可能性があるため、ID引きが成立した場合はJellyfinへIDと
marketplaceを必ず対で保存する。

## Response schema

実装に必要な範囲で次のfieldを確認した。

- Album: `id`, `title`, `copyright`, `trackCount`, `duration`, `releaseDate`, `format`,
  `audioQualities`, `images`, `contributingArtists`
- Artist: `id`, `name`, `followerCount`, `biography.text`, `images`, `tracks.edgeCount`
- Track: `id`, `title`, `shortTitle`, `releaseDate`, `languageOfPerformance`, `images`,
  `parentalSettings`, `album`, `contributingArtists`
- Image: `url`, `width`, `height`, `imageType`

標準envelopeは `{ "data": { ... }, "errors": [...] }` である。HTTP 200でも `errors` があり
必須dataが欠ける場合はprotocol failureとして扱い、not-foundとしてnegative cacheしては
ならない。明示的なresource nullまたは空検索だけをnot-foundとする。

## Artwork

GraphQLは具体的な `images[].url` を返す。Apple Musicのようなサイズplaceholderではない。
検索converterは `artOriginal.URL` をそのままURLにし、width/heightへ1400を後付けする場合が
あるため、寸法がサーバー実測値とは限らない。

URLはopaqueとして一切加工しない。Amazon固有に見える `_SX` / `_SL` などの変換を推測で
適用しない。

## エラーと負荷制御

- 429はnot-foundではなく専用例外として伝え、cacheしない。
- 401/403はbootstrapを一度だけ更新して再送し、再失敗時は例外にする。
- GraphQL error、壊れたJSON、5xxもnegative cacheしない。
- ID引きを直列化し、間隔を空ける。検索を実装する場合も同じ制御に通す。
- marketplace fallbackは明示的not-found/空結果のときだけ行う。
- 429の閾値や `Retry-After` の挙動は未確認であり、live testは少数のrequestに留める。

## Bundle変更時の再調査

1. JP/USの公開Web Playerからentry HTMLとguest configを一時領域へ取得する。
2. `main.<hash>.js` とconfig内の `dragonflyBundle` を特定する。
3. 詳細取得operation、匿名header生成、endpoint解決とREST検索handlerを静的に確認する。
4. 実測値をredactした最小fixtureだけを更新する。
5. opt-in live testでbootstrapとID round-tripを確認する。検索は匿名tokenの取得経路が成立した場合だけ検証する。
6. 完全bundle、有効なapplication key、Cookie、account token、runtime識別子はコミットしない。

## 未確認事項

- ブラウザNetwork上の最終request/response
- JP/USそれぞれでの全operationのguest許可範囲
- 429閾値と `Retry-After`
- ASINのterritory間互換性
- 自動取得、cache、artwork利用に関する利用条件

実装前提として扱う観測事実と、live testで確認すべき仮説を分け、内部APIの変化を
「カタログに存在しない」という結果へ変換しないことが重要である。
