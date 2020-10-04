FROM microsoft/dotnet:sdk AS build-env
WORKDIR /app
COPY ./src/Abune.Server/ ./Abune.Server/
COPY ./src/Abune.Shared/ ./Abune.Shared/
RUN dotnet restore Abune.Server/Abune.Server.csproj
COPY ./ .
RUN dotnet publish Abune.Server/Abune.Server.csproj -c Release -p:Version=#{GitVersion.SemVer}# -o out

FROM microsoft/dotnet:2.2-runtime
WORKDIR /app
COPY --from=build-env /app/Abune.Server/out/ .
ENTRYPOINT ["dotnet", "Abune.Server.dll"]

