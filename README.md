ticket-toolbox
==============

link-commits
------------
Post commit link comments to mentioned Jira tickets.

`ticket-toolbox link-commits [-v|--verbose] [-n|--dry-run] [refs]`

The `git log` history for the mentioned *refs* (or the current commit)
is inspected and searched for Jira issues mentions. The Jira issues are
fetched and a comment is posted linking all the commits not already
mentioned in the issue description or comments.

**Idempotency:** because previously mentioned comments are not reported
again, the tool is idempotent - running it a second time will not cause
it to re-post the same comments.

jira-ado-sync-links
-------------------
Copies issue links ("blocks", "duplicate of", etc.) from Jira to
Azure DevOps.

Common arguments
----------------
With **-v** or **--verbose**, some debug info is printed.

With **-n** or **--dry-run**, only a preview of the action is given, no
changes are written.

Setup
-----
Jira authentication needs to be configured in your environment. First,
[create an API token](https://id.atlassian.com/manage-profile/security/api-tokens),
then run or add this to your `.bashrc` or such:

    export JIRA_USER=john.doe@example.com

For your secret, you can do one of three things:

 1. Export `JIRA_SECRET` with the token (not recommended).
 2. Export `JIRA_SECRET_COMMAND` with a command that yields the token, e.g.
    using a password manager.
 3. Do nothing; the tool will prompt you for a token every time.

Then, per-repository, with tweaks as appropriate for your repository and
Jira instance:

	git config jira.baseUrl "https://example.atlassian.net"
	git config jira.commitLinkFormat "https://example.com/git/SomeWebapp/commit/{commitHash}"
	git config jira.issueRegex "FOO-[0-9]+"

Compile this project (or download binaries) and put them somewhere in
your `$PATH`.

Example
-------
    > cd C:\code\SomeWebapp
    > ticket-toolbox link-commits

    FOO-171 Enable Linux-based backend development setups
      839837b0 Script to import development databases on Linux
      cc77244f launchSettings: Further align
      f3a01095 launchSettings: Bring Kestrel in line with IIS
    FOO-150 Uploading "large" image attachements fails unhelpfully
      already mentioned: 3e95f073 Don't implicitly use current culture
      already mentioned: 59fcfdfd Update FileSize() tests for later changes
      51b3ce7f Increase file upload limit to 4 MB
      0349b181 Improve file size limit message
  
As you can see, previously mentioned commits are skipped. The resulting
comment on FOO-171:

> *Sijmen J. Mulder, 6 oktober 2022 om 14:47*
> 
> Related commits in SomeWebapp:
>
>  * [839837b0 - Script to import development databases on Linux](https://example.com/git/SomeWebapp/commit/839837b01bd38f0cbeaac03a9cc799dcc420544d)
>  * [cc77244f - launchSettings: Further align](https://example.com/git/SomeWebapp/commit/cc77244fd84cc16e04711cf9ce8ee7a7f7c71f84)
>  * [f3a01095 - launchSettings: Bring Kestrel in line with IIS](https://example.com/git/SomeWebapp/commit/f3a010956802c9c1f065ae6dd794b7834384e437)

Q&A
---
**Is this secure?**

This tool has no dependencies except .NET itself, uses git only locally
and the only information sent to Jira is the posted comment.

You do however need to be careful with your Jira secret. Either set up
`JIRA_SECRET_COMMAND` with something secure like a password manager, or
just let the tool prompt for it.

**Can I try this without actually posting things to Jira?**

Configure as described, then run with the **-n** (or **--dry-run**)
flag.

**Which commits exactly are checked?**

All commits in the history of the current commit, or in the history of
the *refs* parameter.

I'd suggest only using this on *develop*, *master* or *main* and such as
not to link to commits that may be rebased, not end up being merged at
all, etc.

**When is a commit considered 'already mentioned'?**

If the first 8 characters of the commit hash occur in the Jira issue
description or any of its comments, the commit is considered 'already
mentioned' and not included in the new comment.

License
-------
BSD-2, see LICENSE.md.
