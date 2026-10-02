# Frongle

Frongle tracks the street assets of a council or utility (its tenant) and the maintenance and replacement of those assets.

## Language

**Tenant**:
An organization whose data is kept apart from every other organization's data.
_Avoid_: Customer, company, account

**Maintenance Manager**:
A user who sees Asset status, draws Areas, and sets Maintenance Schedules.
_Avoid_: Admin, supervisor

**Work Team**:
A user group that installs, repairs, removes, and replaces Assets in the field.
_Avoid_: Crew, technician

**Asset**:
A physical item in the street that has a type, a location, and a history of Events.
_Avoid_: Equipment, object, device

**Asset Type**:
The kind of Asset: light-post (LP), street sign (SS), telephone pole (TP), or traffic light (TL).
_Avoid_: Category, class

**Friendly Id**:
The tag on the Asset, written AREA-TYPE-NUMBER, for example MN-LP-02213. The system makes it, and it is unique within a Tenant.
_Avoid_: Code, tag number, asset number

**Internal Id**:
The identifier that only Frongle uses for an Asset. People never see it.
_Avoid_: Database id, GUID

**Former Friendly Id**:
A Friendly Id that an Asset carried before a retag. A search still finds the Asset by it.
_Avoid_: Old id, legacy id

**Retag**:
The change of an Asset's Friendly Id, which means that someone must fit a new physical tag.
_Avoid_: Rename, renumber

**Area**:
A named region of the map, drawn by a Maintenance Manager, with a two-letter code. An Asset belongs to the Area that contains its location. Areas of one tenant never overlap, but they can share an edge and they can leave gaps. A location on a shared edge belongs to the Area with the lower code. A location in a gap belongs to no Area, so no Asset can sit there.
_Avoid_: Zone, region, district

**Event**:
A dated record in an Asset's history of one thing that happened to it. It has a type, a title, notes, a date and time, and images.
_Avoid_: Activity, log entry, job

**Event Type**:
The kind of Event: Installed, Checked, Repaired, Maintained, or Removed.
_Avoid_: Action

**Asset Status**:
The state of an Asset, worked out from its Events: Pending Installation, In Service, or Removed.
_Avoid_: State

**Pending Installation**:
The Status of an Asset that has no Events yet.

**Audit Record**:
A permanent entry that says who changed which field of which record, from what value to what value, and when.
_Avoid_: Change log, revision

**Work Order**:
A job that a Maintenance Manager assigns to a Work Team. (Out of scope for now.)

**Maintenance Schedule**:
A plan of the dates on which Assets need work. (Out of scope for now.)
