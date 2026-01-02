FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/McpApi.Core/McpApi.Core.csproj", "src/McpApi.Core/"]
COPY ["src/McpApi.Web/McpApi.Web.csproj", "src/McpApi.Web/"]
RUN dotnet restore "src/McpApi.Web/McpApi.Web.csproj"
COPY . .
WORKDIR "/src/src/McpApi.Web"
RUN dotnet build "McpApi.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "McpApi.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "McpApi.Web.dll"]
