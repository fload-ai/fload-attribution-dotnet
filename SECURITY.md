# Security

Please report a vulnerability to security@fload.com, not in a public issue.
We answer within two working days.

The write key the SDK ships with (`fload_pk_…`) is public by design: it can
only report installs for one app. Fload swaps Apple's token with Apple on the
server, so a copied key cannot fake an Apple Ads attribution.
