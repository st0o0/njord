"""Verify generated proto modules are importable."""


def test_common_pb2_importable():
    from custom_components.njord.proto.njord.v2 import common_pb2

    assert hasattr(common_pb2, "HourlyForecast")
    assert hasattr(common_pb2, "DailyForecast")
    assert hasattr(common_pb2, "LocationInfo")
    assert hasattr(common_pb2, "ModelInfo")


def test_weather_pb2_importable():
    from custom_components.njord.proto.njord.v2 import weather_pb2

    assert hasattr(weather_pb2, "GetCatalogRequest")
    assert hasattr(weather_pb2, "GetCatalogResponse")
    assert hasattr(weather_pb2, "GetForecastResponse")
    assert hasattr(weather_pb2, "ForecastUpdate")


def test_admin_pb2_importable():
    from custom_components.njord.proto.njord.v2 import admin_pb2

    assert hasattr(admin_pb2, "NjordConfig")
    assert hasattr(admin_pb2, "GetConfigRequest")


def test_ops_pb2_importable():
    from custom_components.njord.proto.njord.v2 import ops_pb2

    assert hasattr(ops_pb2, "StatusResponse")
    assert hasattr(ops_pb2, "TriggerPollRequest")


def test_weather_grpc_importable():
    from custom_components.njord.proto.njord.v2 import weather_pb2_grpc

    assert hasattr(weather_pb2_grpc, "WeatherServiceStub")


def test_admin_grpc_importable():
    from custom_components.njord.proto.njord.v2 import admin_pb2_grpc

    assert hasattr(admin_pb2_grpc, "AdminServiceStub")


def test_ops_grpc_importable():
    from custom_components.njord.proto.njord.v2 import ops_pb2_grpc

    assert hasattr(ops_pb2_grpc, "OpsServiceStub")


def test_sensor_pb2_importable():
    from custom_components.njord.proto.njord.v2 import sensor_pb2

    assert hasattr(sensor_pb2, "SensorReading")
    assert hasattr(sensor_pb2, "PushResponse")
    assert hasattr(sensor_pb2, "SENSOR_KIND_INDOOR_TEMPERATURE")
    assert hasattr(sensor_pb2, "SENSOR_KIND_INDOOR_HUMIDITY")


def test_sensor_grpc_importable():
    from custom_components.njord.proto.njord.v2 import sensor_pb2_grpc

    assert hasattr(sensor_pb2_grpc, "SensorServiceStub")
