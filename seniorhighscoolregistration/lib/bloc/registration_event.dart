// lib/bloc/registration_event.dart

import 'package:equatable/equatable.dart';
import '../data/models/registration_model.dart';

abstract class RegistrationEvent extends Equatable {
  const RegistrationEvent();

  @override
  List<Object?> get props => [];
}

class LoadRegistrations extends RegistrationEvent {
  const LoadRegistrations();
}

class SearchRegistrations extends RegistrationEvent {
  final String query;
  const SearchRegistrations(this.query);

  @override
  List<Object?> get props => [query];
}

class SubmitRegistration extends RegistrationEvent {
  final RegistrationModel registration;
  const SubmitRegistration(this.registration);

  @override
  List<Object?> get props => [registration];
}

class UpdateRegistration extends RegistrationEvent {
  final RegistrationModel registration;
  const UpdateRegistration(this.registration);

  @override
  List<Object?> get props => [registration];
}

class DeleteRegistration extends RegistrationEvent {
  final int id;
  const DeleteRegistration(this.id);

  @override
  List<Object?> get props => [id];
}

class PickDocumentFile extends RegistrationEvent {
  const PickDocumentFile();
}

class ClearPickedDocument extends RegistrationEvent {
  const ClearPickedDocument();
}

class LoadDashboardStats extends RegistrationEvent {
  const LoadDashboardStats();
}