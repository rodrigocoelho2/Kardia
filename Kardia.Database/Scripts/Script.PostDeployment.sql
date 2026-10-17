/*
    Post-deployment script of Kardia.Database.
    Runs after every publish of the dacpac. There is no reference data to seed: accounts are created from the Website
    (the first one becomes the admin) and every account fills itself from its Apple Watch syncs.
*/
PRINT N'Kardia.Database deployed.';
