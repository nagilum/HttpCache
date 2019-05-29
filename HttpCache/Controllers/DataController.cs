using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HttpCache.Controllers {
    [Route("api/[controller]")]
    [ApiController]
    public class DataController : ControllerBase {
        /// <summary>
        /// Get data from the cache storage.
        /// </summary>
        [HttpGet]
        public ActionResult Get() {
            // Get and verify owner.
            var owner = this.Request.Headers.Keys.Contains("x-httpcache-owner")
                ? this.Request.Headers["x-httpcache-owner"].ToString().ToLower()
                : null;

            if (string.IsNullOrWhiteSpace(owner)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-owner' is required."
                });
            }

            // Check for data.
            if (Program.Storage == null) {
                return this.NotFound(new {
                    message = "No storage container defined yet."
                });
            }

            if (!Program.Storage.ContainsKey(owner)) {
                return this.NotFound(new {
                    message = "Owner not found."
                });
            }

            // Get info about all keys?
            if (!this.Request.Headers.Keys.Contains("x-httpcache-key")) {
                var list = Program.Storage[owner]
                    .Select(n => n.Value)
                    .ToList();

                return this.Ok(
                    list
                        .Where(n => !n.HasExpired)
                        .Select(n => new {
                            n.Created,
                            n.Updated,
                            n.LastRead,
                            n.Expires,
                            n.ExpiryLength,
                            n.SlidingExpiration,
                            n.Size,
                            n.Key,
                            n.ContentType
                        }));
            }

            // Get and verify key.
            var key = this.Request.Headers.Keys.Contains("x-httpcache-key")
                ? this.Request.Headers["x-httpcache-key"].ToString().ToLower()
                : null;

            if (string.IsNullOrWhiteSpace(key)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-key' cannot be empty."
                });
            }

            if (!Program.Storage[owner].ContainsKey(key)) {
                return this.NotFound(new {
                    message = "Key not found."
                });
            }

            // Found data.
            var entry = Program.Storage[owner][key];

            if (entry.HasExpired) {
                return this.NotFound(new {
                    message = "Data has expired."
                });
            }

            // Update last-read.
            entry.LastRead = DateTimeOffset.Now;
            
            // Update expiration if sliding is enabled.
            if (entry.SlidingExpiration &&
                entry.ExpiryLength.HasValue) {

                entry.Expires = DateTimeOffset.Now.AddSeconds(entry.ExpiryLength.Value);
            }

            // Output data.
            if (this.Request.Headers.Keys.Contains("x-httpcache-return-only-data")) {
                this.Response.ContentType = entry.ContentType;
                this.Response.WriteAsync(entry.Value);

                return this.Ok();
            }
            else {
                return this.Ok(entry);
            }
        }

        /// <summary>
        /// Check if data exists in the cache storage.
        /// </summary>
        [HttpHead]
        public ActionResult Exists() {
            // Get and verify owner.
            var owner = this.Request.Headers.Keys.Contains("x-httpcache-owner")
                ? this.Request.Headers["x-httpcache-owner"].ToString().ToLower()
                : null;

            if (string.IsNullOrWhiteSpace(owner)) {
                return this.BadRequest();
            }

            // Check for data.
            if (Program.Storage == null) {
                return this.NotFound();
            }

            if (!Program.Storage.ContainsKey(owner)) {
                return this.NotFound();
            }

            // Get and verify key.
            var key = this.Request.Headers.Keys.Contains("x-httpcache-key")
                ? this.Request.Headers["x-httpcache-key"].ToString().ToLower()
                : null;

            if (string.IsNullOrWhiteSpace(key)) {
                return this.BadRequest();
            }

            if (!Program.Storage[owner].ContainsKey(key)) {
                return this.NotFound();
            }

            // Found data.
            var entry = Program.Storage[owner][key];

            if (entry.HasExpired) {
                return this.NotFound();
            }

            // Update last-read.
            entry.LastRead = DateTimeOffset.Now;

            // Update expiration if sliding is enabled.
            if (entry.SlidingExpiration &&
                entry.ExpiryLength.HasValue) {

                entry.Expires = DateTimeOffset.Now.AddSeconds(entry.ExpiryLength.Value);
            }

            // Output data.
            return this.Ok();
        }

        /// <summary>
        /// Save data to the cache storage.
        /// </summary>
        [HttpPost]
        public ActionResult Set() {
            // Get and verify owner.
            var owner = this.Request.Headers.Keys.Contains("x-httpcache-owner")
                ? this.Request.Headers["x-httpcache-owner"].ToString()
                : null;

            if (string.IsNullOrWhiteSpace(owner)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-owner' is required."
                });
            }

            // Get and verify key.
            var key = this.Request.Headers.Keys.Contains("x-httpcache-key")
                ? this.Request.Headers["x-httpcache-key"].ToString()
                : null;

            if (string.IsNullOrWhiteSpace(key)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-key' is required."
                });
            }

            // Get expiry length.
            int? expiryLength = null;

            if (this.Request.Headers.Keys.Contains("x-httpcache-expiry-length")) {
                if (int.TryParse(this.Request.Headers["x-httpcache-expiry-length"].ToString(), out var tempEL)) {
                    expiryLength = tempEL;
                }
                else {
                    return this.BadRequest(new {
                        message = "Header 'x-httpcache-expiry-length' is the number of seconds to keep data."
                    });
                }
            }

            // Get sliding expiration.
            bool? slidingExpiration = null;

            if (this.Request.Headers.Keys.Contains("x-httpcache-sliding-expiration")) {
                if (bool.TryParse(this.Request.Headers["x-httpcache-sliding-expiration"].ToString(), out var tempSE)) {
                    slidingExpiration = tempSE;
                }
                else {
                    return this.BadRequest(new {
                        message = "Header 'x-httpcache-sliding-expiration' is a boolean value."
                    });
                }
            }

            string body = null;
            string contentType = null;

            // Get body from external URL.
            if (this.Request.Headers.Keys.Contains("x-httpcache-url")) {
                try {
                    var url = this.Request.Headers["x-httpcache-url"].ToString();
                    var req = WebRequest.Create(url) as HttpWebRequest;

                    if (req == null) {
                        throw new Exception(
                            string.Format(
                                "Could not create HttpWebRequest for URL: {0}",
                                url));
                    }

                    var timeout = 3000;

                    if (this.Request.Headers.Keys.Contains("x-httpcache-url-timeout")) {
                        if (int.TryParse(this.Request.Headers["x-httpcache-url-timeout"].ToString(), out var tempTO)) {
                            timeout = tempTO;
                        }
                        else {
                            return this.BadRequest(new {
                                message = "Header 'x-httpcache-url-timeout' is a number of milliseconds for timeout."
                            });
                        }
                    }

                    req.Timeout = timeout;
                    req.UserAgent = "HttpCache Auto-Update Data-Fetcher";
                    req.Method = this.Request.Headers.Keys.Contains("x-httpcache-url-method")
                        ? this.Request.Headers["x-httpcache-url-method"].ToString().ToUpper()
                        : "GET";

                    var res = req.GetResponse() as HttpWebResponse;

                    if (res == null) {
                        throw new Exception(
                            string.Format(
                                "Could not create HttpWebResponse from HttpWebRequest for URL: {0}",
                                url));
                    }

                    foreach (var resHeaderKey in res.Headers.AllKeys) {
                        if (resHeaderKey.ToLower() != "content-type") {
                            continue;
                        }

                        contentType = res.Headers[resHeaderKey];
                    }

                    var stream = res.GetResponseStream();

                    if (stream == null) {
                        throw new Exception(
                            string.Format(
                                "Could not get ResponseStream from HttpWebResponse for URL: {0}",
                                url));
                    }

                    body = new StreamReader(stream).ReadToEnd();
                }
                catch (Exception ex) {
                    return this.BadRequest(new {
                        message = "Unable to get data from external URL.",
                        exception = new {
                            message = ex.Message,
                            serialized = JsonConvert.SerializeObject(ex)
                        }
                    });
                }
            }

            // Get body from request.
            if (body == null) {
                try {
                    body = new StreamReader(this.Request.Body)
                        .ReadToEnd();
                }
                catch (Exception ex) {
                    return this.BadRequest(new {
                        message = "Unable to parse request body as string.",
                        exception = new {
                            message = ex.Message,
                            serialized = JsonConvert.SerializeObject(ex)
                        }
                    });
                }
            }

            if (contentType == null &&
                this.Request.Headers.ContainsKey("x-httpcache-content-type")) {

                contentType = this.Request.Headers["x-httpcache-content-type"].ToString();
            }

            if (string.IsNullOrWhiteSpace(contentType)) {
                contentType = "text/plain";
            }

            if (string.IsNullOrWhiteSpace(body)) {
                return this.BadRequest(new {
                    message = "Request body is empty. If you ment to delete the entry, use the DELETE HTTP method."
                });
            }

            // Prepare storage.
            if (Program.Storage == null) { 
                Program.Storage = new Dictionary<string, Dictionary<string, CacheEntry>>();
            }

            if (!Program.Storage.ContainsKey(owner.ToLower())) {
                Program.Storage.Add(owner.ToLower(), new Dictionary<string, CacheEntry>());
            }

            // Get/create entry.
            var entry = Program.Storage[owner.ToLower()].ContainsKey(key.ToLower())
                ? Program.Storage[owner.ToLower()][key.ToLower()]
                : new CacheEntry();

            // Update/set data.
            entry.Updated = DateTimeOffset.Now;
            entry.Value = body;
            entry.ContentType = contentType;
            entry.ExpiryLength = expiryLength;

            if (expiryLength.HasValue) {
                entry.Expires = DateTimeOffset.Now.AddSeconds(expiryLength.Value);
            }

            if (expiryLength.HasValue &&
                expiryLength.Value == -1) {

                entry.ExpiryLength = null;
                entry.Expires = null;
            }

            if (slidingExpiration.HasValue) {
                entry.SlidingExpiration = slidingExpiration.Value;
            }

            if (entry.InStorage) {
                return this.Ok(new {
                    entry.Created,
                    entry.Updated,
                    entry.LastRead,
                    entry.Expires,
                    entry.ExpiryLength,
                    entry.SlidingExpiration,
                    entry.Size,
                    entry.ContentType
                });
            }

            Program.Storage[owner.ToLower()].Add(key.ToLower(), entry);

            entry.Owner = owner;
            entry.Key = key;
            entry.InStorage = true;

            return this.Ok(new {
                entry.Created,
                entry.Updated,
                entry.LastRead,
                entry.Expires,
                entry.ExpiryLength,
                entry.SlidingExpiration,
                entry.Size,
                entry.ContentType
            });
        }

        /// <summary>
        /// Update expiration for existing entry.
        /// </summary>
        [HttpPut]
        public ActionResult Update() {
            // Get and verify owner.
            var owner = this.Request.Headers.Keys.Contains("x-httpcache-owner")
                ? this.Request.Headers["x-httpcache-owner"].ToString()
                : null;

            if (string.IsNullOrWhiteSpace(owner)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-owner' is required."
                });
            }

            // Get and verify key.
            var key = this.Request.Headers.Keys.Contains("x-httpcache-key")
                ? this.Request.Headers["x-httpcache-key"].ToString()
                : null;

            if (string.IsNullOrWhiteSpace(key)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-key' is required."
                });
            }

            // Check for data.
            if (Program.Storage == null) {
                return this.NotFound(new {
                    message = "No storage container defined yet."
                });
            }

            if (!Program.Storage.ContainsKey(owner)) {
                return this.NotFound(new {
                    message = "Owner not found."
                });
            }

            if (!Program.Storage[owner].ContainsKey(key)) {
                return this.NotFound(new {
                    message = "Key not found."
                });
            }

            // Found data.
            var entry = Program.Storage[owner][key];

            if (entry.HasExpired) {
                return this.NotFound(new {
                    message = "Data has expired."
                });
            }

            // Get expiry length.
            int? expiryLength = null;

            if (this.Request.Headers.Keys.Contains("x-httpcache-expiry-length")) {
                if (int.TryParse(this.Request.Headers["x-httpcache-expiry-length"].ToString(), out var tempEL)) {
                    expiryLength = tempEL;
                }
                else {
                    return this.BadRequest(new {
                        message = "Header 'x-httpcache-expiry-length' is the number of seconds to keep data."
                    });
                }
            }

            entry.Updated = DateTimeOffset.Now;
            entry.ExpiryLength = expiryLength;

            if (expiryLength.HasValue) {
                entry.Expires = DateTimeOffset.Now.AddSeconds(expiryLength.Value);
            }

            if (expiryLength.HasValue &&
                expiryLength.Value == -1) {

                entry.ExpiryLength = null;
                entry.Expires = null;
            }

            return this.Ok(new {
                entry.Created,
                entry.Updated,
                entry.LastRead,
                entry.Expires,
                entry.ExpiryLength,
                entry.SlidingExpiration,
                entry.Size,
                entry.ContentType
            });
        }

        /// <summary>
        /// Delete data from the cache storage.
        /// </summary>
        [HttpDelete]
        public ActionResult Delete() {
            // Get and verify owner.
            var owner = this.Request.Headers.Keys.Contains("x-httpcache-owner")
                ? this.Request.Headers["x-httpcache-owner"].ToString().ToLower()
                : null;

            if (string.IsNullOrWhiteSpace(owner)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-owner' is required."
                });
            }

            // Get and verify key.
            var key = this.Request.Headers.Keys.Contains("x-httpcache-key")
                ? this.Request.Headers["x-httpcache-key"].ToString().ToLower()
                : null;

            if (string.IsNullOrWhiteSpace(key)) {
                return this.BadRequest(new {
                    message = "Header 'x-httpcache-key' is required."
                });
            }

            // Check for data.
            if (Program.Storage == null) {
                return this.NotFound(new {
                    message = "No storage container defined yet."
                });
            }

            if (!Program.Storage.ContainsKey(owner)) {
                return this.NotFound(new {
                    message = "Owner not found."
                });
            }

            string message;

            // Are we looking for 1 item or several?
            if (this.Request.Headers.Keys.Contains("x-httpcache-key-is-prefix")) {
                var keys = Program.Storage[owner].Keys.ToList();
                var count = 0;

                foreach (var ownerKey in keys) {
                    if (!ownerKey.StartsWith(key)) {
                        continue;
                    }

                    Program.Storage[ownerKey].Remove(ownerKey);
                    count++;
                }

                message = string.Format(
                    "{0} entries removed.",
                    count);
            }
            else {
                if (!Program.Storage[owner].ContainsKey(key)) {
                    return this.NotFound(new {
                        message = "Key not found."
                    });
                }

                Program.Storage[owner].Remove(key);

                message = "Entry removed.";
            }

            // Cleanup
            if (Program.Storage[owner].Count == 0) {
                Program.Storage.Remove(owner);
            }

            // Report back.
            return this.Ok(new {
                message
            });
        }
    }
}