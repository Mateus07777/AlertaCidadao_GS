FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AlertaCidadao/AlertaCidadao.csproj AlertaCidadao/
RUN dotnet restore AlertaCidadao/AlertaCidadao.csproj
COPY AlertaCidadao/ AlertaCidadao/
RUN dotnet publish AlertaCidadao/AlertaCidadao.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AlertaCidadao.dll"]
