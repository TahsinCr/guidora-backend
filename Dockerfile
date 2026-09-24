FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["NetCoreBackend.sln", "."]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Entities/Entities.csproj", "Entities/"]
COPY ["DataAccess/DataAccess.csproj", "DataAccess/"]
COPY ["Business/Business.csproj", "Business/"]
COPY ["WebAPI/WebAPI.csproj", "WebAPI/"]
RUN dotnet restore "WebAPI/WebAPI.csproj"

COPY . .
WORKDIR "/src/WebAPI"
RUN dotnet publish "WebAPI.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache curl
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=5 \
  CMD curl --fail --silent http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "WebAPI.dll"]
