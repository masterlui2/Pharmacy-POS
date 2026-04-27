// lib/ui/screens/registration_screen.dart

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

import '../../bloc/registration_bloc.dart';
import '../../bloc/registration_event.dart';
import '../../bloc/registration_state.dart';
import '../../data/models/registration_model.dart';
import 'package:seniorhighscoolregistration/constants.dart';
import '../widgets/app_dropdown.dart';
import '../widgets/document_picker_tile.dart';

class RegistrationScreen extends StatefulWidget {
  const RegistrationScreen({super.key});

  @override
  State<RegistrationScreen> createState() => _RegistrationScreenState();
}

class _RegistrationScreenState extends State<RegistrationScreen> {
  final _formKey = GlobalKey<FormState>();

  String? _selectedStaff;
  String? _selectedStrand;
  String? _pickedFileName;

  final _studentNameController = TextEditingController();
  final String _today = DateFormat('MMMM dd, yyyy').format(DateTime.now());

  @override
  void dispose() {
    _studentNameController.dispose();
    super.dispose();
  }

  void _submitForm() {
    if (!_formKey.currentState!.validate()) return;

    final registration = RegistrationModel(
      staffName: _selectedStaff!,
      studentName: _studentNameController.text.trim(),
      date: _today,
      strand: _selectedStrand!,
    );

    context.read<RegistrationBloc>().add(SubmitRegistration(registration));
  }

  void _resetForm() {
    _formKey.currentState?.reset();
    _studentNameController.clear();
    setState(() {
      _selectedStaff = null;
      _selectedStrand = null;
      _pickedFileName = null;
    });
    context.read<RegistrationBloc>().add(const ClearPickedDocument());
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return BlocListener<RegistrationBloc, RegistrationState>(
      listener: (context, state) {
        if (state is DocumentPicked) {
          setState(() => _pickedFileName = state.fileName);
        }
        if (state is DocumentCleared) {
          setState(() => _pickedFileName = null);
        }
        if (state is RegistrationSuccess) {
          _resetForm();
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Row(
                children: [
                  const Icon(Icons.check_circle, color: Colors.white),
                  const SizedBox(width: 8),
                  Text(state.message),
                ],
              ),
              backgroundColor: Colors.green.shade700,
              behavior: SnackBarBehavior.floating,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
              ),
            ),
          );
        }
        if (state is RegistrationError) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Row(
                children: [
                  const Icon(Icons.error_outline, color: Colors.white),
                  const SizedBox(width: 8),
                  Expanded(child: Text(state.message)),
                ],
              ),
              backgroundColor: colorScheme.error,
              behavior: SnackBarBehavior.floating,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
              ),
            ),
          );
        }
      },
      child: Scaffold(
        appBar: AppBar(
          title: const Text('New Registration'),
          centerTitle: false,
          elevation: 0,
          scrolledUnderElevation: 1,
        ),
        body: BlocBuilder<RegistrationBloc, RegistrationState>(
          builder: (context, state) {
            final isSubmitting = state is RegistrationSubmitting;

            return SingleChildScrollView(
              padding: const EdgeInsets.all(20),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // Header card
                    Container(
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            colorScheme.primaryContainer,
                            colorScheme.primaryContainer.withOpacity(0.5),
                          ],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: Row(
                        children: [
                          Icon(
                            Icons.school_outlined,
                            color: colorScheme.onPrimaryContainer,
                            size: 32,
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'Student Registration Form',
                                  style: theme.textTheme.titleMedium?.copyWith(
                                    color: colorScheme.onPrimaryContainer,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                Text(
                                  _today,
                                  style: theme.textTheme.bodySmall?.copyWith(
                                    color: colorScheme.onPrimaryContainer
                                        .withOpacity(0.7),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),

                    const SizedBox(height: 24),

                    // Staff dropdown
                    AppDropdown(
                      label: 'Staff Name',
                      value: _selectedStaff,
                      items: AppConstants.staffNames,
                      onChanged: (v) => setState(() => _selectedStaff = v),
                      hint: 'Select staff',
                      prefixIcon: Icons.badge_outlined,
                    ),

                    const SizedBox(height: 20),

                    // Student name field
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Student Name',
                          style: theme.textTheme.labelLarge?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 8),
                        TextFormField(
                          controller: _studentNameController,
                          textCapitalization: TextCapitalization.words,
                          decoration: InputDecoration(
                            hintText: 'Enter full name',
                            prefixIcon: Icon(
                              Icons.person_outline,
                              color: colorScheme.primary,
                            ),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                              borderSide: BorderSide(
                                color: colorScheme.outline.withOpacity(0.5),
                              ),
                            ),
                            focusedBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(12),
                              borderSide: BorderSide(
                                color: colorScheme.primary,
                                width: 2,
                              ),
                            ),
                            filled: true,
                            fillColor: colorScheme.surfaceContainerHighest
                                .withOpacity(0.3),
                          ),
                          validator: (v) => (v == null || v.trim().isEmpty)
                              ? 'Please enter student name'
                              : null,
                        ),
                      ],
                    ),

                    const SizedBox(height: 20),

                    // Date (read-only)
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Date',
                          style: theme.textTheme.labelLarge?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(height: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 16,
                            vertical: 14,
                          ),
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(12),
                            border: Border.all(
                              color: colorScheme.outline.withOpacity(0.3),
                            ),
                            color: colorScheme.surfaceContainerHighest
                                .withOpacity(0.2),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                Icons.today_outlined,
                                color: colorScheme.primary,
                              ),
                              const SizedBox(width: 12),
                              Text(
                                _today,
                                style: theme.textTheme.bodyMedium?.copyWith(
                                  color: colorScheme.onSurface,
                                ),
                              ),
                              const Spacer(),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 8,
                                  vertical: 2,
                                ),
                                decoration: BoxDecoration(
                                  color: colorScheme.primaryContainer,
                                  borderRadius: BorderRadius.circular(6),
                                ),
                                child: Text(
                                  'Today',
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: colorScheme.onPrimaryContainer,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),

                    const SizedBox(height: 20),

                    // STRAND dropdown
                    AppDropdown(
                      label: 'STRAND',
                      value: _selectedStrand,
                      items: AppConstants.strands,
                      onChanged: (v) => setState(() => _selectedStrand = v),
                      hint: 'Select strand',
                      prefixIcon: Icons.menu_book_outlined,
                    ),

                    const SizedBox(height: 20),

                    // Document uploader
                    DocumentPickerTile(
                      fileName: _pickedFileName,
                      onPick: () => context
                          .read<RegistrationBloc>()
                          .add(const PickDocumentFile()),
                      onClear: () => context
                          .read<RegistrationBloc>()
                          .add(const ClearPickedDocument()),
                    ),

                    const SizedBox(height: 32),

                    // Submit button
                    FilledButton(
                      onPressed: isSubmitting ? null : _submitForm,
                      style: FilledButton.styleFrom(
                        minimumSize: const Size.fromHeight(52),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(14),
                        ),
                      ),
                      child: isSubmitting
                          ? const SizedBox(
                        width: 22,
                        height: 22,
                        child: CircularProgressIndicator(
                          strokeWidth: 2.5,
                          color: Colors.white,
                        ),
                      )
                          : const Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(Icons.check_circle_outline),
                          SizedBox(width: 8),
                          Text(
                            'Submit Registration',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      ),
                    ),

                    const SizedBox(height: 12),

                    // Clear button
                    OutlinedButton(
                      onPressed: isSubmitting ? null : _resetForm,
                      style: OutlinedButton.styleFrom(
                        minimumSize: const Size.fromHeight(48),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(14),
                        ),
                      ),
                      child: const Text('Clear Form'),
                    ),

                    const SizedBox(height: 24),
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}
