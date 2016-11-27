﻿// ==========================================================================
//  LazyClientStore.cs
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex Group
//  All rights reserved.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Options;
using Squidex.Infrastructure;
using Squidex.Read.Apps;
using Squidex.Read.Apps.Services;

namespace Squidex.Config.Identity
{
    public class LazyClientStore : IClientStore
    {
        private readonly IAppProvider appProvider;
        private readonly Dictionary<string, Client> staticClients = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);

        public LazyClientStore(IOptions<MyUrlsOptions> urlsOptions, IAppProvider appProvider)
        {
            Guard.NotNull(urlsOptions, nameof(urlsOptions));
            Guard.NotNull(appProvider, nameof(appProvider));

            this.appProvider = appProvider;

            CreateStaticClients(urlsOptions);
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            var client = staticClients.GetOrDefault(clientId);

            if (client != null)
            {
                return client;
            }

            var token = clientId.Split(':');

            if (token.Length != 2)
            {
                return null;
            }

            var app = await appProvider.FindAppByNameAsync(token[0]);

            var appClient = app?.Clients.FirstOrDefault(x => x.ClientName == token[1]);

            if (appClient == null)
            {
                return null;

            }

            client = CreateClientFromApp(clientId, appClient);

            return client;
        }

        private static Client CreateClientFromApp(string id, IAppClientEntity appClient)
        {
            return new Client
            {
                ClientId = id,
                ClientName = id,
                ClientSecrets = new List<Secret> { new Secret(appClient.ClientSecret.Sha512(), appClient.ExpiresUtc) },
                AccessTokenLifetime = (int)TimeSpan.FromDays(30).TotalSeconds,
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = new List<string>
                {
                    Constants.ApiScope
                }
            };
        }

        private void CreateStaticClients(IOptions<MyUrlsOptions> urlsOptions)
        {
            foreach (var client in CreateStaticClients(urlsOptions.Value))
            {
                staticClients[client.ClientId] = client;
            }
        }

        private static IEnumerable<Client> CreateStaticClients(MyUrlsOptions urlsOptions)
        {
            const string id = Constants.FrontendClient;

            yield return new Client
            {
                ClientId = id,
                ClientName = id,
                RedirectUris = new List<string>
                {
                    urlsOptions.BuildUrl("login;"),
                    urlsOptions.BuildUrl("identity-server/client-callback-silent/"),
                    urlsOptions.BuildUrl("identity-server/client-callback-popup/")
                },
                PostLogoutRedirectUris = new List<string>
                {
                    urlsOptions.BuildUrl("logout", false)
                },
                AllowAccessTokensViaBrowser = true,
                AllowedGrantTypes = GrantTypes.Implicit,
                AllowedScopes = new List<string>
                {
                    StandardScopes.OpenId.Name,
                    StandardScopes.Profile.Name,
                    Constants.ApiScope,
                    Constants.ProfileScope
                },
                RequireConsent = false
            };
        }
    }
}
