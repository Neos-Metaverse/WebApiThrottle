﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace WebApiThrottle
{
    public class EndpointRule
    {
        public readonly Regex pathRegex;
        public readonly HttpMethod method;
        public readonly RateLimits limits;

        public EndpointRule(string pathRegex, HttpMethod method, RateLimits limits)
            : this(new Regex(pathRegex), method, limits)
        {

        }

        public EndpointRule(Regex pathRegex, HttpMethod method, RateLimits limits)
        {
            this.pathRegex = pathRegex;
            this.method = method;
            this.limits = limits;
        }

        public bool Match(RequestIdentity request)
        {
            if (!pathRegex.IsMatch(request.Endpoint))
                return false;

            if (method != null && method != request.Method)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Rate limits policy
    /// </summary>
    [Serializable]
    public class ThrottlePolicy
    {
        public ThrottlePolicy()
        {
            IpWhitelist = new List<string>();
            IpRules = new Dictionary<string, RateLimits>();
            ClientWhitelist = new List<string>();
            ClientRules = new Dictionary<string, RateLimits>();
            EndpointWhitelist = new List<string>();
            EndpointRules = new List<EndpointRule>();
            Rates = new Dictionary<RateLimitPeriod, long>();
        }

        /// <summary>
        /// Configure default request limits per second, minute, hour or day
        /// </summary>
        public ThrottlePolicy(long? perSecond = null, long? perMinute = null, long? perHour = null, long? perDay = null, long? perWeek = null)
            : this()
        {
            Rates = new Dictionary<RateLimitPeriod, long>();
            if (perSecond.HasValue)
            {
                Rates.Add(RateLimitPeriod.Second, perSecond.Value);
            }

            if (perMinute.HasValue)
            {
                Rates.Add(RateLimitPeriod.Minute, perMinute.Value);
            }


            if (perHour.HasValue)
            {
                Rates.Add(RateLimitPeriod.Hour, perHour.Value);
            }


            if (perDay.HasValue)
            {
                Rates.Add(RateLimitPeriod.Day, perDay.Value);
            }
            if (perWeek.HasValue)
            {
                Rates.Add(RateLimitPeriod.Week, perWeek.Value);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether IP throttling is enabled.
        /// </summary>
        public bool IpThrottling { get; set; }

        public List<string> IpWhitelist { get; set; }

        public IDictionary<string, RateLimits> IpRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether client key throttling is enabled.
        /// </summary>
        public bool ClientThrottling { get; set; }

        public List<string> ClientWhitelist { get; set; }

        public IDictionary<string, RateLimits> ClientRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether route throttling is enabled
        /// </summary>
        public bool EndpointThrottling { get; set; }

        public List<string> EndpointWhitelist { get; set; }

        public List<EndpointRule> EndpointRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all requests, including the rejected ones, should be stacked in this order: day, hour, min, sec
        /// </summary>
        public bool StackBlockedRequests { get; set; }

        public Dictionary<RateLimitPeriod, long> Rates { get; set; }

        public static ThrottlePolicy FromStore(IThrottlePolicyProvider provider)
        {
            var settings = provider.ReadSettings();
            var whitelists = provider.AllWhitelists();
            var rules = provider.AllRules();

            var policy = new ThrottlePolicy(
                perSecond: settings.LimitPerSecond,
               perMinute: settings.LimitPerMinute,
               perHour: settings.LimitPerHour,
               perDay: settings.LimitPerDay,
               perWeek: settings.LimitPerWeek);

            policy.IpThrottling = settings.IpThrottling;
            policy.ClientThrottling = settings.ClientThrottling;
            policy.EndpointThrottling = settings.EndpointThrottling;
            policy.StackBlockedRequests = settings.StackBlockedRequests;

            policy.IpRules = new Dictionary<string, RateLimits>();
            policy.ClientRules = new Dictionary<string, RateLimits>();
            policy.EndpointRules = new List<EndpointRule>();
            policy.EndpointWhitelist = new List<string>();
            policy.IpWhitelist = new List<string>();
            policy.ClientWhitelist = new List<string>();

            foreach (var item in rules)
            {
                var rateLimit = new RateLimits
                                    {
                                        PerSecond = item.LimitPerSecond,
                                        PerMinute = item.LimitPerMinute,
                                        PerHour = item.LimitPerHour,
                                        PerDay = item.LimitPerDay,
                                        PerWeek = item.LimitPerWeek
                                    };

                switch (item.PolicyType)
                {
                    case ThrottlePolicyType.IpThrottling:
                        policy.IpRules.Add(item.Entry, rateLimit);
                        break;
                    case ThrottlePolicyType.ClientThrottling:
                        policy.ClientRules.Add(item.Entry, rateLimit);
                        break;
                    case ThrottlePolicyType.EndpointThrottling:
                        policy.EndpointRules.Add(new EndpointRule(item.Entry, null, rateLimit));
                        break;
                }
            }

            if (whitelists != null)
            {
                policy.IpWhitelist.AddRange(whitelists.Where(x => x.PolicyType == ThrottlePolicyType.IpThrottling).Select(x => x.Entry));
                policy.ClientWhitelist.AddRange(whitelists.Where(x => x.PolicyType == ThrottlePolicyType.ClientThrottling).Select(x => x.Entry));
                policy.EndpointWhitelist.AddRange(whitelists.Where(x => x.PolicyType == ThrottlePolicyType.EndpointThrottling).Select(x => x.Entry));
            }
            return policy;
        }
    }
}
