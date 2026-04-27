// lib/data/models/registration_model.dart

class RegistrationModel {
  final int? id;
  final String staffName;
  final String studentName;
  final String date;
  final String strand;
  final String? documentPath;
  final String? documentName;

  const RegistrationModel({
    this.id,
    required this.staffName,
    required this.studentName,
    required this.date,
    required this.strand,
    this.documentPath,
    this.documentName,
  });

  Map<String, dynamic> toMap() {
    return {
      if (id != null) 'id': id,
      'staffName': staffName,
      'studentName': studentName,
      'date': date,
      'strand': strand,
      'documentPath': documentPath,
      'documentName': documentName,
    };
  }

  factory RegistrationModel.fromMap(Map<String, dynamic> map) {
    return RegistrationModel(
      id: map['id'] as int?,
      staffName: map['staffName'] as String,
      studentName: map['studentName'] as String,
      date: map['date'] as String,
      strand: map['strand'] as String,
      documentPath: map['documentPath'] as String?,
      documentName: map['documentName'] as String?,
    );
  }

  RegistrationModel copyWith({
    int? id,
    String? staffName,
    String? studentName,
    String? date,
    String? strand,
    String? documentPath,
    String? documentName,
  }) {
    return RegistrationModel(
      id: id ?? this.id,
      staffName: staffName ?? this.staffName,
      studentName: studentName ?? this.studentName,
      date: date ?? this.date,
      strand: strand ?? this.strand,
      documentPath: documentPath ?? this.documentPath,
      documentName: documentName ?? this.documentName,
    );
  }
}