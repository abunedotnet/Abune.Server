//-----------------------------------------------------------------------
// <copyright file="MetricsConfig.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Config
{
     /// <summary>Configuration entry for metrics.</summary>
    public class MetricsConfig
    {
        /// <summary>Gets or sets the interval seconds.</summary>
        /// <value>The interval seconds.</value>
        public int IntervalSeconds { get; set; } = 60;
    }
}
