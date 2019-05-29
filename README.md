# HttpCache

HttpCache is a simple ASP.NET Core Web API which acts as a key/value storage for data. It has a single API endpoint `/api/data` which answers to five HTTP methods; `GET`, `HEAD`, `POST`, `PUT`, and `DELETE`. It is meant to serve multiple applications with cache data, so all entries are separated by an "owner".

## Store Data
There are two ways of storing data in HttpCache, either by posting the data directly to the API, or tell the API where to get the data from an external source. Both are done with the `POST` HTTP method to the API.

### Storing Data Directly
To store data, either way, you need to include two headers with your request. `x-httpcache-owner` and `x-httpcache-key`. The "owner" header tells the API into which vault to store the data. The "key" is the key itself to the data.

If you include those two headers and a payload with the `POST` request, that's it.

You can include the `x-httpcache-content-type` header to tell the API what kind of mime-type to use when giving the data back directly.

### Storing Data From External Source
If you have another API somewhere that provides the actual data, you don't need to post it yourself, you can tell the API where to get it. Include the `x-httpcache-owner` and `x-httpcache-key` as normal, but also include `x-httpcache-url` with an URL to the remote data. The data will be downloaded and stored under the given owner/key.

The default timeout used towards the remote source is 3000 (3 seconds) which can be overridden by including your own number in the `x-httpcache-url-timeout` header.

The default HTTP method used towards the remote source is `GET` which can be overridden by including your own HTTP method in the `x-httpcache-url-method` header.

When fetching data from an external source, the content-type is saved from that, but can be overridden by including the `x-httpcache-content-type` header.

### Expiration
By default the data posted does not expire. You can add expiration by including the header `x-httpcache-expiry-length` with a number of seconds the data should remain alive in storage. If you want to remove expiration after it's been set, include the same header, but set the value to `-1` (minus 1).

If you want to enable sliding expiration, just include the header `x-httpcache-sliding-expiration` with the value `true`. If you want to remove sliding expiration, include the same header, but with the value `false`.

## Update Expiration
You can update expiration on an existing item by using the `PUT` HTTP method. You can also do it with the `POST` method, but that requires you to post the data as well.

Include the `x-httpcache-owner` and `x-httpcache-key` headers as normal, but also the `x-httpcache-expiry-length` header to either set or remove expiration. You can also include the `x-httpcache-sliding-expiration` header to update that.

## Get Data
There are three ways to retrieve data from the API with the `GET` HTTP method.

### Get A List of All Keys for a Owner
If you just want a list of all keys (with metadata) related to a owner, include the `x-httpcache-owner` header. This will give back a list of all keys with the stored metadata.

The response will look something like this:
```json
[
    {
        "created": "2019-05-29T13:22:07.1875863+02:00",
        "updated": "2019-05-29T13:22:07.1876591+02:00",
        "lastRead": null,
        "expires": "2019-05-29T13:38:47.1878949+02:00",
        "expiryLength": 1000,
        "slidingExpiration": false,
        "size": 99206,
        "key": "Products",
        "contentType": "application/json; charset=utf-8"
    }
]
```

### Get The Storage Entry
If you include the `x-httpcache-key` header as well, you will only get that single item back, but then a `value` will also be present with the actual data.

Example:
```json
{
    "created": "2019-05-29T13:22:07.1875863+02:00",
    "updated": "2019-05-29T13:22:07.1876591+02:00",
    "lastRead": null,
    "expires": "2019-05-29T13:38:47.1878949+02:00",
    "expiryLength": 1000,
    "slidingExpiration": false,
    "size": 40,
    "key": "Products",
    "contentType": "application/json; charset=utf-8",
    "value": "[{\"id\":123,\"name\":\"Test Product\"}]"
}
```

### Get The Direct Data
If you just want the actual data back, you can include the `x-httpcache-return-only-data` header. This tells the API to use the saved content-type and just serve the data. The content-type is fetched from the external source if that was used, or it is set to `text/plain` by default. This can be overridden with the `x-httpcache-content-type` header when storing data.

In the case of the example data above, the output would then look like this:
```json
{
    "id": 123,
    "name": "Test Product"
}
```

## Check if Data is Available
If you just want to check if data exists in storage, use the `HEAD` HTTP method. All the same rules as `GET` applies, but you will only get either `200` Ok (data exists), `400` Bad Request, or `404` Not Found as response.

## Delete Data
There are three ways of deleting data, all are using the `DELETE` HTTP method, and all require the `x-httpcache-owner` header.

Either way, you will get a list back of all the keys deleted, like so:
```json
{
    "keysDeleted": [
        "key1",
        "key2"
    ]
}
```

### Delete Entire Owner
If you only include the owner header, you will delete all keys stored under that owner vault.

### Delete Specific Key
If you include the `x-httpcache-key` header, you will delete that specific key.

### Delete Keys Based On Prefix
If you include the key header as well as `x-httpcache-key-is-prefix` you will delete all keys starting with the given key-name.

## All HTTP Methods
* `GET` Get stored data.
* `HEAD` Check if keys exists.
* `POST` Create/update stored data.
* `PUT` Update expiration for stored data.
* `DELETE` Delete existing data.

## All Headers
* `x-httpcache-owner` *(string)* The owner of the data. Can be viewed as a parent to the keys.
* `x-httpcache-key` *(string)* The key data is stored under.
* `x-httpcache-key-is-prefix` *(bool)* Used to specify whether given key is entire key or prefix.
* `x-httpcache-expiry-length` *(int)* Set/unset the length of expiration, in seconds.
* `x-httpcache-sliding-expiration` *(bool)* Set/unset sliding expiration.
* `x-httpcache-url` *(string)* Fetch data for storage from an external source.
* `x-httpcache-url-timeout` *(int)* Set the timeout for remote source, in milliseconds.
* `x-httpcache-url-method` *(string)* Set the HTTP method to use for external source.
* `x-httpcache-content-type` *(string)* Set content-type to store data under.
* `x-httpcache-return-only-data` *(bool)* Return only the stored data, not the cache entry object.

## All Response Codes
* `200` Everything is Ok.
* `400` You did something wrong. Read the message given back.
* `404` Data not found.
