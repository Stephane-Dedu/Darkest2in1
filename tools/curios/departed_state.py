"""Source-proved empty used states for three departed curio figures only."""
import hashlib
from pathlib import Path

from PIL import Image


SEMANTICS = "departed_after_investigation"
APPROVED_REFERENCES = {
    "cc_shrew_hooded": "c5457dfa1c5d02d4e06df5af4962c31e6a4fa8608722aa6857dfe955de48ffa2",
    "cc_shrew_elder": "896c3ea5cecc34a90b2fca4048d7d3d5702159ac64805592d7725ac0953dc02e",
    "com_millerwife_curio": "f95f8d1af2b2b0f0eda2420ac53edb1eaee6c7c42b128ebc8837a1497c711015",
}


def departure_proof(metadata, base):
    """Validate the explicit policy and a retained proper terminal reference."""
    semantics = metadata.get("used_state_semantics")
    proof = metadata.get("departure_proof")
    if semantics is None:
        if proof is not None:
            raise ValueError("A departure proof requires explicit used-state semantics")
        return None
    if semantics != SEMANTICS or not isinstance(proof, dict):
        raise ValueError("Unsupported used-state semantics or missing departure proof")
    identity = proof.get("curio_id")
    expected = APPROVED_REFERENCES.get(identity)
    if expected is None or metadata.get("id") != identity + "_design":
        raise ValueError("Only the three source-reviewed departed figures are allowed")
    if proof.get("animation") != "investigated" or proof.get("reference_sha256") != expected:
        raise ValueError("Use the reviewed investigated terminal reference")
    name = proof.get("reference_file")
    if not isinstance(name, str) or not name:
        raise ValueError("Missing retained terminal reference file")
    path = (Path(base) / name).resolve()
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
        raise ValueError("Retained terminal reference changed")
    with Image.open(path) as reference:
        if reference.mode != "RGBA" or reference.getchannel("A").getextrema() != (0, 0):
            raise ValueError("The actual terminal reference must be fully departed")
    return dict(curio_id=identity, animation="investigated",
                reference_file=str(path), reference_sha256=expected)
