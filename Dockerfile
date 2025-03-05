# Use a multi-stage build to build and publish the .NET application
# Specify the initial base architecture (amd64 in this case)
#FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0.401 AS build-env
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /repo/src

# Copy everything else and build
COPY ./src/ /repo/src/

COPY ./LICENSE.TXT /repo/LICENSE.txt

RUN dotnet clean
RUN dotnet restore
RUN dotnet build  ./AasxServerBlazor/AasxServerBlazor.csproj  -c Release -o /build/AasxServerBlazor

# publish the build project 
FROM build-env AS publish
RUN dotnet publish ./AasxServerBlazor/AasxServerBlazor.csproj -c Release -o /publish/AasxServerBlazor --no-restore


# Use a runtime image to run the application
# Specify the initial base architecture (amd64 in this case)

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /AasxServerBlazor
COPY --from=publish /publish/AasxServerBlazor/ .
COPY ./content-for-demo/ .

RUN apt update && apt upgrade --yes
RUN apt install -y curl nano libgdiplus
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_HTTP_PORTS=5001
EXPOSE 5001/tcp
EXPOSE 5001/udp

#ENTRYPOINT ["dotnet", "AasxServerBlazor.dll"]
ENTRYPOINT ["dotnet", "AasxServerBlazor.dll", "--async", "--no-security", "--secret-string-api", "1234", "--data-path", "./aasxs", "--host", "0.0.0.0"]


