FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Zrobim.Api/Zrobim.Api.csproj src/Zrobim.Api/
RUN dotnet restore src/Zrobim.Api/Zrobim.Api.csproj
COPY src/Zrobim.Api/ src/Zrobim.Api/
RUN dotnet publish src/Zrobim.Api/Zrobim.Api.csproj -c Release --no-restore -p:UseAppHost=false -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publish/ ./
COPY db/demo/catalog.sql demo/catalog.sql
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
USER app
ENTRYPOINT ["dotnet", "Zrobim.Api.dll"]
