FROM microsoft/aspnetcore:2.0
WORKDIR pub
COPY pub .
EXPOSE 5003
ENTRYPOINT ["dotnet", "IdentityWs.dll"]
