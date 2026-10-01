# Derive Asset Status from Events

Asset Status is worked out from the Event history (no Events means Pending Installation, a latest Event of Removed means Removed, anything else means In Service) and is never stored. A stored status could disagree with the history. The cost is a query over Events when we list Assets. The latest Event is the one with the latest date and time. If two Events have the same date and time, the one with the higher Id is the latest, so the Status is always the same.
