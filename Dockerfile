FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PharmacyPOS.csproj", "./"]
RUN dotnet restore "PharmacyPOS.csproj"

COPY . .
RUN dotnet publish "PharmacyPOS.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 10000

ENTRYPOINT ["dotnet", "PharmacyPOS.dll"]
