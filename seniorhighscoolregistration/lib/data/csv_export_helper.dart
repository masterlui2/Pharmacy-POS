// lib/data/csv_export_helper.dart

import 'dart:io';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';
import 'models/registration_model.dart';

class CsvExportHelper {
  static Future<void> exportRegistrations(
    List<RegistrationModel> registrations,
  ) async {
    final buffer = StringBuffer();

    // Header row
    buffer.writeln('ID,Staff Name,Student Name,Date,Strand,Document');

    // Data rows — wrap fields with commas in quotes
    for (final reg in registrations) {
      final id = reg.id ?? '';
      final staff = _escapeCsv(reg.staffName);
      final student = _escapeCsv(reg.studentName);
      final date = _escapeCsv(reg.date);
      final strand = reg.strand;
      final doc = _escapeCsv(reg.documentName ?? '');
      buffer.writeln('$id,$staff,$student,$date,$strand,$doc');
    }

    // Write to temp file
    final dir = await getTemporaryDirectory();
    final now = DateTime.now();
    final filename =
        'registrations_${now.year}${_pad(now.month)}${_pad(now.day)}_${_pad(now.hour)}${_pad(now.minute)}.csv';
    final file = File('${dir.path}/$filename');
    await file.writeAsString(buffer.toString());

    // Share
    await Share.shareXFiles(
      [XFile(file.path, mimeType: 'text/csv')],
      subject: 'Student Registrations Export',
    );
  }

  static String _escapeCsv(String value) {
    if (value.contains(',') || value.contains('"') || value.contains('\n')) {
      return '"${value.replaceAll('"', '""')}"';
    }
    return value;
  }

  static String _pad(int n) => n.toString().padLeft(2, '0');
}
