-- #######################################
-- #       Drop All Identity Tables      #
-- #######################################

DROP TABLE [dbo].[AspNetUserRoles]

DROP TABLE [dbo].[AspNetRoles]

DROP TABLE [dbo].[AspNetUserClaims]

DROP TABLE [dbo].[AspNetUserLogins] 

DROP TABLE [dbo].[AspNetUsers]

-- #######################################
-- #    Drop All Users/Profile Tables    #
-- #######################################

ALTER TABLE [dbo].[User_Show]  DROP CONSTRAINT [FK_dbo.User_Show_dbo.Musicians_Id] 

ALTER TABLE [dbo].[User_Show]  DROP CONSTRAINT [FK_dbo.User_Show_dbo.VenueOwners_Id] 

ALTER TABLE [dbo].[User_Show]  DROP CONSTRAINT [FK_dbo.User_Show_dbo.Shows_Id]

ALTER TABLE [dbo].[Shows]  DROP CONSTRAINT [FK_dbo.Shows_dbo.Venues_Id]

DROP TABLE [dbo].[User_Show]

DROP TABLE [dbo].[Shows] 

-- #######################################
-- #    Drop Ratings Table			     #
-- #######################################
ALTER TABLE [dbo].[Ratings] DROP CONSTRAINT [FK_dbo.Ratings_dbo.Users_Id]

DROP TABLE [dbo].[Ratings]

-- #######################################
-- #    Drop All Users/Profile Tables    #
-- #######################################
ALTER TABLE [dbo].[Videos]  DROP CONSTRAINT [FK_dbo.Videos_dbo.Users_ID]

ALTER TABLE [dbo].[Photos]  DROP CONSTRAINT [FK_dbo.Photos_dbo.Users_ID]

ALTER TABLE [dbo].[Venues]	DROP CONSTRAINT [FK_dbo.Venues_dbo.Users_ID]

ALTER TABLE [dbo].[Musician_Genre]  DROP CONSTRAINT [FK_dbo.Musician_Genre_dbo.Users_ID]

ALTER TABLE [dbo].[Musician_Genre]  DROP CONSTRAINT [FK_dbo.Musician_Genre_dbo.Genres_ID]

/*ALTER TABLE [dbo].[BandMember_Instrument]  DROP CONSTRAINT [FK_dbo.BandMember_Instrument_dbo.BandMembers_ID]

ALTER TABLE [dbo].[BandMember_Instrument]  DROP CONSTRAINT [FK_dbo.BandMember_Instrument_dbo.Instruments_ID]

ALTER TABLE [dbo].[BandMembers]  DROP CONSTRAINT [FK_dbo.BandMembers_dbo.Users_ID] */

ALTER TABLE [dbo].[Venues]  DROP CONSTRAINT [FK_dbo.Venues_dbo.VenueTypes_ID]

DROP TABLE [dbo].[Videos]

DROP TABLE [dbo].[Photos]

DROP TABLE [dbo].[Musician_Genre]

DROP TABLE [dbo].[Genres]

DROP TABLE [dbo].[Profiles]

/*
DROP TABLE [dbo].[BandMember_Instrument]

DROP TABLE [dbo].[Instruments]

DROP TABLE [dbo].[BandMembers] */

DROP TABLE [dbo].[Venues]

DROP TABLE [dbo].[VenueTypes]

DROP TABLE [dbo].[Users]

-- DROP TABLE [dbo].[Roles]
