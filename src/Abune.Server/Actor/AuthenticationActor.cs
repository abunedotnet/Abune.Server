//-----------------------------------------------------------------------
// <copyright file="AuthenticationActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor
{
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security;
    using System.Text;
    using Abune.Server.Actor.Command;
    using Abune.Shared.Command;
    using Akka.Actor;
    using Akka.Event;
    using Microsoft.IdentityModel.Tokens;

    /// <summary>Actor for client authentication.</summary>
    /// <seealso cref="Akka.Actor.ReceiveActor" />
    public class AuthenticationActor : ReceiveActor
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private readonly JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        private readonly string auth0Issuer;
        private readonly string auth0Audience;
        private readonly SecurityKey signingKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationActor"/> class.
        /// </summary>
        /// <param name="auth0Issuer">The auth0 domain.</param>
        /// <param name="auth0Audience">The auth0 audience.</param>
        /// <param name="signingKey">The signing key.</param>
        public AuthenticationActor(string auth0Issuer, string auth0Audience, string signingKey)
        {
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new ArgumentOutOfRangeException(nameof(signingKey));
            }

            if (string.IsNullOrWhiteSpace(auth0Issuer))
            {
                throw new ArgumentOutOfRangeException(nameof(auth0Issuer));
            }

            if (string.IsNullOrWhiteSpace(auth0Audience))
            {
                throw new ArgumentOutOfRangeException(nameof(auth0Audience));
            }

            this.auth0Issuer = auth0Issuer;
            this.auth0Audience = auth0Audience;
            this.signingKey = new SymmetricSecurityKey(Convert.FromBase64String(signingKey.ToString()));
            this.Receive<RequestAuthenticationCommand>(c =>
            {
                this.ProcessAuthenticationRequest(c.ReplyTo, c.AuthenticationChallenge, c.Token);
            });
        }

        private void ProcessAuthenticationRequest(IActorRef replyTo, string authenticationChallenge, string token)
        {
            var validationParameters =
                new TokenValidationParameters
                {
                    ValidIssuer = this.auth0Issuer,
                    ValidAudiences = new[] { this.auth0Audience },
                    IssuerSigningKeys = new[] { this.signingKey },
                    ValidateLifetime = false,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                };

            /* To see detailed exception information:
             * Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
             */
            SecurityToken securityToken;
            try
            {
                var claimsPrincipal = this.tokenHandler.ValidateToken(token, validationParameters, out securityToken);
                if (!claimsPrincipal.HasClaim(m => m.Type == Shared.Constants.Auth.JwtClaims.AUTHENTICATIONCHALLENGE && m.Value == authenticationChallenge))
                {
                    throw new InvalidOperationException(Shared.Constants.Auth.JwtClaims.AUTHENTICATIONCHALLENGE);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception excp)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                replyTo.Tell(new AuthenticationFailure()
                {
                    Error = excp.Message,
                });
                return;
            }

            replyTo.Tell(new AuthenticationSuccess());
            this.log.Debug("success");
        }
    }
}
