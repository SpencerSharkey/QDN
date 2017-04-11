using Newtonsoft.Json;

namespace QDN
{
    class Settings
    {
        [JsonProperty]
        public bool Debug = false;

        // State whitelist
        [JsonProperty]
        public bool EnableSubdivisionWhitelist = false;

        [JsonProperty]
        public string[] SubdivisionWhitelist = { };

        // State blacklist
        [JsonProperty]
        public bool EnableSubdivisionBlacklist = false;

        [JsonProperty]
        public string[] SubdivisionBlacklist = { };

        // Continent whitelist
        [JsonProperty]
        public bool EnableContinentWhitelist = false;

        [JsonProperty]
        public string[] ContinentWhitelist = { };

        // Continent blacklist
        [JsonProperty]
        public bool EnableContinentBlacklist = false;

        [JsonProperty]
        public string[] ContinentBlacklist = { };
    }
}
