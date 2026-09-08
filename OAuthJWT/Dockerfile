FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["OAuthJWT.csproj", "./"]

RUN dotnet restore "OAuthJWT.csproj"

COPY . .

RUN dotnet publish "OAuthJWT.csproj" -c Release -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "OAuthJWT.dll"]