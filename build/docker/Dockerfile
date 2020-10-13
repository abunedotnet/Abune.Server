FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app
COPY ./src/Abune.Server/ ./Abune.Server/
COPY ./src/Abune.Shared/ ./Abune.Shared/
RUN dotnet restore Abune.Server/Abune.Server.csproj
COPY ./ .
RUN dotnet publish Abune.Server/Abune.Server.csproj -c Release -p:Version=#{GitVersion.SemVer}# -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
COPY --from=build-env /app/out/ .
ENTRYPOINT ["dotnet", "Abune.Server.dll"]

