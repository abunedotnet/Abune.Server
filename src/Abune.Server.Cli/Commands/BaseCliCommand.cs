//-----------------------------------------------------------------------
// <copyright file="BaseCliCommand.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Cli.Commands
{
    using System;
    using System.Globalization;
    using System.IO;

    /// <summary>Abstract base class for cli commands.</summary>
    public abstract class BaseCliCommand
    {
        /// <summary>Gets or sets the host.</summary>
        /// <value>The host.</value>
        protected string Host { get; set; }
        /// <summary>Gets or sets the port.</summary>
        /// <value>The port.</value>
        protected int Port { get; set; }
        /// <summary>Gets or sets the log.</summary>
        /// <value>The log.</value>
        protected TextWriter Log { get; set; }

        /// <summary>Initializes a new instance of the <a onclick="return false;" href="BaseCliCommand" originaltag="see">BaseCliCommand</a> class.</summary>
        /// <param name="log">The log.</param>
        /// <param name="parameters">The parameters.</param>
        /// <exception cref="ArgumentOutOfRangeException">parameters</exception>
        protected BaseCliCommand(TextWriter log, string[] parameters)
        {
            if (parameters == null || parameters.Length != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters));
            }
            Log = log;
            Host = parameters[0];
            Port = int.Parse(parameters[1], CultureInfo.InvariantCulture);
        }
    }
}
