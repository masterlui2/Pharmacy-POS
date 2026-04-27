import 'package:flutter/material.dart';
import 'package:seniorhighscoolregistration/data/models/registration_model.dart';

class RegistrationDetailScreen extends StatelessWidget {
  final RegistrationModel registration;

  const RegistrationDetailScreen({
    super.key,
    required this.registration,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Registration Details'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: ListView(
          children: [
            _buildItem('Student Name', registration.studentName),
            _buildItem('Strand', registration.strand),
          ],
        ),
      ),
    );
  }

  Widget _buildItem(String label, dynamic value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Card(
        child: ListTile(
          title: Text(label),
          subtitle: Text(value?.toString() ?? ''),
        ),
      ),
    );
  }
}
