namespace Property_Maintenance_API.Domain;

public enum RequestStatus { Abierta, Asignada, EnProgreso, Cerrada, Cancelada }

public enum RequestPriority { Baja, Media, Alta, Urgente }

public enum RequestCategory { Plomeria, Electricidad, General }

public enum UnitType { Departamento, Local }

public enum TechnicianSpecialty { Plomeria, Electricidad, General }