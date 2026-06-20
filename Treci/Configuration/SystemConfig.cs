using Akka.Configuration;

namespace Treci
{
    public static class SystemConfig
    {
        public static Config GetAkkaConfig() =>
            ConfigurationFactory.ParseString(@"
            yelp-dispatcher {
                type = Dispatcher
                executor = ""fork-join-executor""
                fork-join-executor {
                    parallelism-min = 4
                    parallelism-factor = 2.0
                    parallelism-max = 16
                }
                throughput = 100
            }");
    }
}