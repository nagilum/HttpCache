using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace HttpCache {
    public class Program {
        /// <summary>
        /// Storage container for owner => kvs.
        /// </summary>
        public static Dictionary<string, Dictionary<string, CacheEntry>> Storage { get; set; }

        /// <summary>
        /// Init all the things..
        /// </summary>
        public static void Main(string[] args) {
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build()
                .Run();
        }
    }
}