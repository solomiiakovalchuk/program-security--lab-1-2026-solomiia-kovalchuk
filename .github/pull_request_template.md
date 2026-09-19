## Description

<!-- What changed and why. 1-3 sentences. -->

## Change type

- [ ] `DEV` - new feature
- [ ] `FIX` - defect fix
- [ ] `SEC` - security related change
- [ ] `DOCS` - documentation only
- [ ] `RELEASE` - release

## Related requirements

<!-- IDs from docs/specification/software_requirements.md: FR-NN, NFR-NN, SEC-NN, CON-NN. -->

-

## Checklist

- [ ] Tests added or updated, `dotnet test` passes (or explain why tests do not apply)
- [ ] Docs updated: `docs/specification/modules/<Module>/business_logic.md` if the module changed
- [ ] No secrets, tokens, `credentials.json`, `*.db`, `settings.json` or personal data in code and commit history
- [ ] Layer boundaries kept (Core does not depend on Data/Documents/Integration/Desktop; ViewModels do not use Google types)
- [ ] New public types and members have XML doc comments
- [ ] Commit messages use the format `[TYPE] summary`
