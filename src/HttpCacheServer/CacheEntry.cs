using System;
using Newtonsoft.Json;

namespace HttpCache {
    public class CacheEntry {
        #region Entry Properties

        public DateTimeOffset Created { get; set; }

        public DateTimeOffset Updated { get; set; }

        public DateTimeOffset? LastRead { get; set; }

        public DateTimeOffset? Expires { get; set; }

        public int? ExpiryLength { get; set; }

        public bool SlidingExpiration { get; set; }

        public string Owner { get; set; }

        public string Key { get; set; }

        public string Value { get; set; }

        public string ContentType { get; set; }

        [JsonIgnore]
        public bool InStorage { get; set; }

        #endregion

        #region Helper Properties

        [JsonIgnore]
        public bool HasExpired {
            get {
                if (!this.Expires.HasValue) {
                    return false;
                }

                return this.Expires.Value < DateTimeOffset.Now;
            }
        }

        public int? Size {
            get {
                if (this.Value == null) {
                    return null;
                }

                return this.Value.Length;
            }
        }

        #endregion

        public CacheEntry() {
            this.Created = DateTimeOffset.Now;
        }
    }
}