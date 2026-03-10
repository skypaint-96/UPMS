let
    ApiBaseUrl = "http://localhost:8081/api/v1",
    ApiKey = "upms-dev-key",
    Source = Json.Document(
        Web.Contents(
            ApiBaseUrl & "/tickets?itsmSource=servicenow-prod&company=Contoso",
            [
                Headers = [#"X-UPMS-API-Key" = ApiKey]
            ]
        )
    ),
    Tickets = Table.FromRecords(Source)
in
    Tickets
