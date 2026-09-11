FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props .
COPY MyDigitalLibrary.slnx .
COPY src/MyDigitalLibrary.Domain/MyDigitalLibrary.Domain.csproj src/MyDigitalLibrary.Domain/
COPY src/MyDigitalLibrary.Application/MyDigitalLibrary.Application.csproj src/MyDigitalLibrary.Application/
COPY src/MyDigitalLibrary.Infrastructure/MyDigitalLibrary.Infrastructure.csproj src/MyDigitalLibrary.Infrastructure/
COPY src/MyDigitalLibrary.Api/MyDigitalLibrary.Api.csproj src/MyDigitalLibrary.Api/
RUN dotnet restore src/MyDigitalLibrary.Api/MyDigitalLibrary.Api.csproj

COPY src/MyDigitalLibrary.Domain/ src/MyDigitalLibrary.Domain/
COPY src/MyDigitalLibrary.Application/ src/MyDigitalLibrary.Application/
COPY src/MyDigitalLibrary.Infrastructure/ src/MyDigitalLibrary.Infrastructure/
COPY src/MyDigitalLibrary.Api/ src/MyDigitalLibrary.Api/
RUN dotnet publish src/MyDigitalLibrary.Api/MyDigitalLibrary.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyDigitalLibrary.Api.dll"]
