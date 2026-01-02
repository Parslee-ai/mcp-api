FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/AnyAPI.Core/AnyAPI.Core.csproj", "src/AnyAPI.Core/"]
COPY ["src/AnyAPI.Web/AnyAPI.Web.csproj", "src/AnyAPI.Web/"]
RUN dotnet restore "src/AnyAPI.Web/AnyAPI.Web.csproj"
COPY . .
WORKDIR "/src/src/AnyAPI.Web"
RUN dotnet build "AnyAPI.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AnyAPI.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "AnyAPI.Web.dll"]
