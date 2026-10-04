"""Generated protobuf/gRPC stubs for njord API."""

import os
import sys

# grpc_tools.protoc generates imports using the proto package path (e.g.
# "from njord.v2 import ...") rather than the Python package path. Adding
# this directory to sys.path lets those imports resolve.
_proto_dir = os.path.dirname(__file__)
if _proto_dir not in sys.path:
    sys.path.insert(0, _proto_dir)
