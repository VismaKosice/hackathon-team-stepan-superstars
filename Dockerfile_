# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Install NativeAOT build prerequisites
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       clang zlib1g-dev

WORKDIR /source

# copy csproj and restore as distinct layers
COPY HackatonAPI/*.csproj ./HackatonAPI/
RUN dotnet restore ./HackatonAPI/HackatonAPI.csproj

# copy everything else and build app
COPY HackatonAPI/. ./HackatonAPI/
WORKDIR /source/HackatonAPI/
RUN dotnet publish -c release -o /app --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

# Set default port to 8080, can be overridden with PORT environment variable
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HackatonAPI.dll"]