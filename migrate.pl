#!/usr/bin/perl -w
#
# You are advised to run this script on a temporary copy of your tree.
# It defaults to migrating to the "master" branch - pass the name of another
# branch to migrate to that instead.

use FindBin qw($Bin);

if (!-e ".bzr") {
    print "Current directory is not the root of a bzr tree.\n";
    exit(1);
}

if (!-f $Bin . "/FastImportRewriter.exe") {
    print "Please compile FastImportRewriter.cs first ('mcs FastImportWriter.cs').\n";
    exit(1);
}

my $branch = $ARGV[0] || "master";

system("git init");
system("bzr fast-export --git-branch=$branch --no-plain | mono $Bin/FastImportRewriter.exe | git fast-import");
unlink(".bzr");
system("git reset --hard");

exit(0);
