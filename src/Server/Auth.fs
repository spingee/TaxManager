module Auth

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Https
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.Certificate
open Saturn

type Saturn.Application.ApplicationBuilder with
    [<CustomOperation("use_client_certificate")>]
    member __.UseClientCertificate(state: ApplicationState) =
        let middleware (app: IApplicationBuilder) = app.UseAuthentication()

        let service (s: IServiceCollection) =
            s.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
                .AddCertificate()       // The default implementation uses a memory cache.
                .AddCertificateCache()
                |> ignore
            s

        let webHostBuilder (webHost: IWebHostBuilder) =
            webHost.ConfigureKestrel(fun o ->
                o.ConfigureHttpsDefaults(fun u ->
                    //u.ServerCertificate
                    let lol() = 1 + 1
                    lol() |> ignore
                    u.ClientCertificateMode <- ClientCertificateMode.RequireCertificate))

        { state with
              ServicesConfig = service :: state.ServicesConfig
              AppConfigs = middleware :: state.AppConfigs
              WebHostConfigs = webHostBuilder :: state.WebHostConfigs
              CookiesAlreadyAdded = true }