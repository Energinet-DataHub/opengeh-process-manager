workspace "Name" "Description" {

    !identifiers hierarchical

    model {
        processManager = softwareSystem "Process Manager" {
            webApp = container "Web Application" {
                tags "web application"
            }
            orchestrations = container "Orchestrations" {
                tags "Azure function, C#"
            }
            db = container "Database Schema" {
                tags "Database"
            }
            BlobStorage = container "Blob Storage" {
                tags "Azure Blob Storage"
            }
            webApp -> BlobStorage "writes to start orchestrations"
        }
        processManager.webApp -> processManager.db "Reads from and writes to"
        processManager.webApp -> processManager.BlobStorage "Schedual orchestrations"

        processManager.orchestrations -> processManager.db "Reads from and writes to"
        processManager.orchestrations -> processManager.BlobStorage "Reads from and writes to, internal state"

    }

    views {

        container processManager "process_manager" {
            include *
            autolayout lr
        }

        styles {
            element "Element" {
                color #ffffff
            }
            element "Person" {
                background #ba1e75
                shape person
            }
            element "Software System" {
                background #d92389
            }
            element "Container" {
                background #f8289c
            }
                element "Database" {
                shape cylinder
            }
        }
    }

    configuration {
        scope softwaresystem
    }

}