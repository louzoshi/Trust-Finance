# Two stages so the image ships the runtime and the app, not the SDK and the sources.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore on its own layer: the project files change far less often than the code, so
# a rebuild after an edit skips straight to compiling.
COPY global.json ./
COPY Trust-Finance.sln ./
COPY Trust-Finance.Domain/*.csproj Trust-Finance.Domain/
COPY Trust-Finance.Data/*.csproj Trust-Finance.Data/
COPY Trust-Finance.App/*.csproj Trust-Finance.App/
COPY Trust-Finance.Tests/*.csproj Trust-Finance.Tests/
COPY Trust-Finance.IntegrationTests/*.csproj Trust-Finance.IntegrationTests/
COPY Trust-Finance.E2ETests/*.csproj Trust-Finance.E2ETests/
RUN dotnet restore Trust-Finance.App/Trust-Finance.App.csproj

COPY . .
RUN dotnet publish Trust-Finance.App/Trust-Finance.App.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Where the SQLite file lives. Mount a volume here to keep data across deploys; leave it
# on the container's own filesystem and the demo reseeds itself on every restart.
RUN mkdir -p /data
ENV ConnectionStrings__Default="Data Source=/data/trustfinance.db"

ENV ASPNETCORE_HTTP_PORTS=8080
ENV Browser__Launch=false
EXPOSE 8080

# The runtime image already ships a non-root account ("app", uid 1654); the data
# directory has to belong to it, since SQLite writes there.
RUN chown -R $APP_UID:$APP_UID /data /app
USER $APP_UID

ENTRYPOINT ["dotnet", "TrustFinance.dll"]
