# Amazon Music Web Player API 調査

調査日: 2026-09-11〜2026-09-12

## 結論

Amazon Musicの公開Web Playerは、未ログインの訪問者にも匿名runtime contextを発行する。
desktop Webの検索は`showSearch` BFFへ、曲・アルバム・アーティストの詳細取得はApollo
GraphQLへ送る。

いずれも公開API契約ではなくWeb Playerの内部実装である。endpoint、payload、schema、headerは
予告なく変わる可能性があるため、unit fixtureとopt-in live testで変化を検出する。

## 調査方法とデータ管理

公開ページ、`POST /config.json?skipToken=false`、公開JavaScript bundleを静的解析し、JPの
詳細取得をlive testした。2026-09-12にはブラウザDevToolsでdesktop検索のrequestを確認した。
ログイン、再生、購入、ライブラリ変更は行っていない。

完全なHTML/bundle/HAR、IP address、有効なapplication key、device/session/request ID、CSRF値、
CookieはGit管理しない。fixtureは無効なダミー値だけを使う。

## Bootstrap

JP Web Playerのguest configから以下を取得する。

- `dragonflyBundle`
- `version`
- `deviceType`, `deviceId`
- `sessionId`
- `displayLanguage`, `musicTerritory`, `marketplaceId`
- `csrf.token`, `csrf.ts`, `csrf.rnd`

`accessToken`と`customerId`は未ログイン時に空である。runtime identifierとCSRF値はメモリ上の
request生成にだけ使い、設定、cache key、ログ、例外へ保存しない。

## Desktop検索

ブラウザ実測で確認したendpointは次である。

```text
POST https://fe.web.skill.music.a2z.com/api/showSearch
Content-Type: text/plain;charset=UTF-8
Origin: https://music.amazon.co.jp
Referer: https://music.amazon.co.jp/
```

bodyはouter JSONで、3 fieldはいずれもJSONを文字列化した値である。

```json
{
  "keyword": "{...}",
  "userHash": "{...}",
  "headers": "{...}"
}
```

- `keyword`: interface discriminatorと検索語
- `userHash`: guestでは`level=LIBRARY_MEMBER`
- `headers`: Web PlayerがBFFへ渡すrequest context

`headers`の主な構造は以下である。

- 空access tokenを持つ`x-amzn-authentication`
- device model/family (`WEBPLAYER` / `WebPlayer`)
- device/session/request ID
- device language、currency、timezone
- application version、timestamp
- config由来CSRF metadata
- music domain、referer、page URL
- Web Player feature flags

これらはHTTP request headerではなく、outer bodyの`headers` JSON文字列内に入る。検索cache keyは
marketplace、locale、検索語だけから作り、session/CSRF/device/request IDを含めない。

検索response schemaはまだlive確認していない。schemaを推測せずraw JSONで受け、opt-in live testで
最小fixtureを確定してからoperation固有parserを実装する。

## Mobile向け別検索経路

公開bundleにはiOS/Android向けTenzing RESTも存在する。

```text
POST https://music.amazon.com/{region}/api/textsearch/search/v1_1/
X-Amz-Target: com.amazon.tenzing.textsearch.v1_1.TenzingTextSearchServiceExternalV1_1.search
x-amz-access-token: <runtime token>
```

これはdesktop `showSearch` BFFとは別契約であり、headerやpayloadを流用しない。Apollo上の
`tenzingTextSearch` documentもクライアント内の抽象であり、GraphQL endpointへ直接送るとremote
schemaに存在せずHTTP 400になる。

## 詳細取得GraphQL

通常endpointは`https://gql.music.amazon.dev`で、country-domain feature flagが有効な場合はJPなら
`https://gql.music.amazon.co.jp`になる。匿名application keyは公開bundleから実行時に取得し、値を
保存・ログ出力しない。

JP anonymous guestで次をlive確認した。

| 用途 | operation | 結果 |
| --- | --- | --- |
| 曲 | `trackMetadata` | HTTP 200、errorなし |
| アルバム | `getAlbumMetadata` | HTTP 200、errorなし |
| 収録曲 | `getAlbumTracks` | HTTP 200、errorなし |
| アーティスト | `getArtistSummary` | HTTP 200、許可fieldではerrorなし |

`album.tracks`はconnectionではなくTrack配列である。Artistの`followerCount`、`biography`、`tracks`は
anonymous guestで権限エラーになるため、`id`、`name`、`images`だけを選択する。

主要匿名header:

- `x-api-key`
- `x-amzn-device-id`, `x-amzn-device-type`, `x-amzn-session-id`
- `music-territory`, `Accept-Language`
- `x-amzn-client-app-version`, `x-amzn-trace-start`
- `Content-Type: application/json`

## Responseとartwork

GraphQL envelopeは`data`と任意の`errors`を持つ。HTTP 200でも必須dataが欠けるerror responseは
protocol failureであり、not-foundとしてnegative cacheしない。

画像は具体的な`images[].url`で返る。検索converterが寸法を後付けする場合があるため、URLを
opaqueとして扱い、`_SX`/`_SL`等を推測で書き換えない。

## エラーと負荷制御

- 429は専用例外として伝え、cacheしない。
- 401/403、5xx、壊れたJSONもnegative cacheしない。
- cache → throttle → networkの順を維持する。
- marketplace fallbackは明示的not-found/空結果だけで行う。
- live testは少数requestに限定する。

## 変更時の再調査

1. guest configと公開bundleを一時領域へ取得する。
2. `showSearch` requestをブラウザNetworkで確認する。
3. 詳細GraphQL operationと匿名header生成を静的確認する。
4. runtime値をredactした最小fixtureだけを更新する。
5. opt-in live testでbootstrap、検索、ID round-tripを確認する。
6. bundle、Cookie、token、runtime identifierをコミットしない。

## 未確認事項

- `showSearch` response schema
- USでの検索BFF contextと詳細operationの許可範囲
- 429閾値と`Retry-After`
- ASINのterritory間互換性
- 自動取得、cache、artwork利用に関する利用条件
