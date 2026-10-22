FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so restore is cached until dependencies change
COPY ESEvents.sln .
COPY src/ESEvents.Domain/ESEvents.Domain.csproj src/ESEvents.Domain/
COPY src/ESEvents.Application/ESEvents.Application.csproj src/ESEvents.Application/
COPY src/ESEvents.Infrastructure/ESEvents.Infrastructure.csproj src/ESEvents.Infrastructure/
COPY src/ESEvents.Web/ESEvents.Web.csproj src/ESEvents.Web/
RUN dotnet restore src/ESEvents.Web/ESEvents.Web.csproj

COPY src/ src/
RUN dotnet publish src/ESEvents.Web/ESEvents.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ESEvents.Web.dll"]
