-- Set CustomState to empty string on StepInstance, since failed steps has a breaking change on how custom state
-- is stored (it is now stored as a serialized JSON string, and not a plain string)
UPDATE [pm].[StepInstance]
    SET [CustomState] = ''
GO
