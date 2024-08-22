ticket-toolbox
==============
Very small toolbox for dealing with git, Jira and Azure DevOps.

 * **link-commits** finds Jira issue mentions in the git history and
   posts "related commits" comments to those issue with links.

 * **jira-ado-sync-links** syncs Jira ticket links to Azure DevOps (highly
   specific indeed - author needed it to fix up a Jira -> ADO migration).

link-commits
------------
Finds Jira issue mentions in the git history and posts "related commits"
comments to those issue with links.

`ticket-toolbox link-commits [-v|--verbose] [-n|--dry-run] [refs]`

The `git log` history for the mentioned *refs* (or the current commit)
is inspected and searched for Jira issues mentions. The Jira issues are
fetched and a comment is posted linking all the commits not already
mentioned in the issue description or comments.

Options:
 - **-v** or **--verbose**: print additional debug info.
 - **-n** or **--dry-run**: only give a preview, don't actually post
   comments.

**Idempotency:** because previously mentioned comments are not reported
again, the tool is idempotent - running it a second time will not cause
it to re-post the same comments.

### Example

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

### Q&A

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

**Is this secure?**

This tool has no dependencies except .NET itself, uses git only locally
and the only information sent to Jira is the posted comment.

You do however need to be careful with your Jira secret. Either set up
`JIRA_SECRET_COMMAND` with something secure like a password manager, or
just let the tool prompt for it.

jira-ado-sync-links
-------------------
Syncs Jira ticket links to Azure DevOps (highly specific indeed - author
needed it to fix up a Jira -> ADO migration).

`ticket-toolbox jira-ado-sync-links [-v|--verbose] [-n|--dry-run]`

Iterates over all Jira issue links, tries to find the matching Azure
DevOps ork items, and creates equivalent relations between them. For
example, "blocked by" becomes a "Successor" relation.

Tickets are matched by looking for the Jira key in the title. Existing
relations are kept and no duplicates created.

Options:
 - **-v** or **--verbose**: print additional debug info.
 - **-n** or **--dry-run**: only give a preview, don't actually create
   the links.

## Example

    > cd C:\code\SomeWebapp
    > ticket-toolbox jira-ado-sync-links

    ...
    FOO-163 (#9881)
      add:  System.LinkTypes.Dependency-Forward #9828 (blocks FOO-103)
      add:  System.LinkTypes.Dependency-Forward #9829 (blocks FOO-104)
      keep: System.LinkTypes.Related #9900 (relates to FOO-185)
      keep: System.LinkTypes.Related #9899 (relates to FOO-183)
    ...

## Q&A

**How are linked types mapped?**

Jira's "clones" and "duplicates" become "Duplicate". Jira's "blocks"
becomes "Successor". All other types are mapped to "Related".

**How are Jira issues and Azure DevOps work items matched?**

Azure DevOps is searched for work items with the Jira issue key (e.g.
FOO-123) in the title and the first result is used.

Configuration
-------------
General configuration:

	git config jira.baseUrl "https://example.atlassian.net"
	git config jira.commitLinkFormat "https://example.com/git/SomeWebapp/commit/{commitHash}"
	git config jira.issueRegex "FOO-[0-9]+"

	git config ado.baseUrl "https://dev.azure.com/example"

Authentication  need to be configured in the environment:

    export JIRA_USER=john.doe@example.com

The tools will prompt for secrets when required, but you can also prefill
them in the environment (not secure!):

    export JIRA_SECRET=...
    export ADO_PAT=...

...or specified with e.g. a password manager:

    export JIRA_SECRET_COMMAND=...
    export ADO_PAT_COMMAND=...

To get tokens:

 - Jira: [create an API token](https://id.atlassian.com/manage-profile/security/api-tokens).
 - Azure DevOps: [create personal access token](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate)


License
-------
BSD-2, see LICENSE.md.
